# Deploying to Azure

Everything here is free tier **except the database**, which costs about **USD 4.90/month**.
Read the caveats before you rely on it — and read "What the free database actually cost us"
below before trying to put it back on the free offer.

---

## What gets created

| Piece | Azure resource | Tier | Cost |
|---|---|---|---|
| Database (both services) | Azure SQL Database | **Basic — 5 DTU, 2 GB** | ~USD 4.90/mo |
| Auth019 + expense API | App Service on Linux | **F1 Free** plan, both apps on it | £0 |
| React SPA | Static Web Apps | Free (TLS included) | £0 |
| CI/CD | GitHub Actions | Free minutes | £0 |

`infra/main.bicep` provisions all of it. `.github/workflows/deploy.yml` builds, deploys and smoke-tests on every merge to `main`.

**Aspire is not deployed.** It orchestrates local development only; in Azure these are plain App Service apps wired together with app settings.

---

## The two constraints that shaped this

**1. Azure gives one free SQL database per subscription.** The app wants two. Both services therefore share a single database under separate schemas — Auth019 owns everything in `auth`, the expense API owns `dbo`. They still share no tables and keep separate migration histories (`auth.__EFMigrationsHistory` and `dbo.__EFMigrationsHistory`). This is a deliberate departure from "each service owns its own database" in [ARCHITECTURE.md](ARCHITECTURE.md), taken to keep the bill at zero.

To split them later: create a second database, point `ConnectionStrings__expensedb` at it, and redeploy. No code change — the schema separation already keeps them apart.

**2. F1 Free is genuinely limited.** 60 CPU-minutes/day, 1 GB RAM shared by both apps, and **no Always On** — the first request after an idle period is slow while the app wakes. Exceed the daily CPU quota and both apps return `403 Quota exceeded` (the app's `state` reads `QuotaExceeded`) until **00:00 UTC**. It is fine for yourself and a handful of testers; it is not fine for real traffic. The upgrade is B1 Linux — $0.02/hour, about **USD 14.60/month** — a one-line `sku` change in the Bicep.

---

## What the free database actually cost us

The database started on the **Azure SQL free offer**: serverless General Purpose, 100,000
vCore-seconds a month. On **14 Sep 2026 at 11:19 UTC** the grant ran out, `AutoPause` parked
the database, and the whole site went down — the SPA still served (it is static) while every
call behind it sat on a SQL connection timeout, so it looked like an endless loading spinner.

**It was not bad luck; the free offer could not have covered this app.** Serverless bills for
time *awake*, not work done, and `autoPauseDelay` cannot go below 60 minutes. So every single
wake costs at least `60 min × 0.5 vCore = 1,800 vCore-seconds`, and 100,000 of them only ever
buys about **55 wakes a month — under two app-opens a day**. F1 cold starts and this
workflow's smoke test each spend one too. Measured burn was ~7,400 vCore-seconds/day.

Paying for that same usage on serverless would be about **USD 39/month** at Southeast Asia
list prices ($0.620892/vCore-hour × ~63 vCore-hours). **Basic is USD 4.90 flat** and, being
always on, also removes the 30–60 second stall auto-pause caused on the first request of the
day. The database is 33 MB, so Basic's 2 GB ceiling is nowhere near binding.

If 5 DTU proves too slow, `sqlServiceObjective` takes `S0` — 10 DTU, 250 GB, ~USD 14.70/month.

**The two failure modes are independent.** A healthy database does not stop F1 from
exhausting its 60 CPU-minutes a day, which is a separate `403` with the app's `state` reading
`QuotaExceeded`, clearing at 00:00 UTC.

### The cost alert

A resource-group-scoped monthly budget, `expensetracker019-monthly`, is set at **USD 15**
with mail on **50% / 80% / 100% actual** and **100% forecasted**. Steady state is the USD 4.90
database, so the first rung at USD 7.50 sits well clear of a normal month — an alert that
fires every month stops being read. The forecast rung is the one that arrives while there is
still time to act.

It is **not** in `main.bicep`, because a budget needs a notification address and this
repository is public. Recreate it with the template below, substituting your own:

```bash
az deployment group create -g expensetracker019-rg --name budget-setup   --template-file budget.bicep   --parameters contactEmails='["you@example.com"]' amount=15 startDate=2026-09-01
```

```bicep
targetScope = 'resourceGroup'
param contactEmails array
param amount int = 15
param startDate string          // first of a month, yyyy-MM-dd

resource budget 'Microsoft.Consumption/budgets@2023-05-01' = {
  name: 'expensetracker019-monthly'
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: { startDate: startDate }
    notifications: {
      Actual50:    { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 50,  contactEmails: contactEmails, thresholdType: 'Actual' }
      Actual80:    { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 80,  contactEmails: contactEmails, thresholdType: 'Actual' }
      Actual100:   { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, contactEmails: contactEmails, thresholdType: 'Actual' }
      Forecast100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, contactEmails: contactEmails, thresholdType: 'Forecasted' }
    }
  }
}
```

Note `az consumption budget show` runs against an older API version and prints `thresholdType`
blank; read it back with `az rest ... ?api-version=2023-05-01` to see the real value.

Raise `amount` if you move the App Service plan to B1 — the database plus B1 is about USD 19.50,
which would breach a USD 15 budget every month.

---

## One-time setup

### 1. Resource group

```bash
az login
az group create --name expensetracker019-rg --location southeastasia
```

### 2. Let GitHub sign in to Azure without a password

Federated credentials (OIDC) — no client secret to store or rotate.

```bash
# Create the identity GitHub will act as
az ad app create --display-name expensetracker019-deploy
APP_ID=$(az ad app list --display-name expensetracker019-deploy --query "[0].appId" -o tsv)
az ad sp create --id "$APP_ID"

# Let it manage the resource group
SUB_ID=$(az account show --query id -o tsv)
az role assignment create --assignee "$APP_ID" --role Contributor \
  --scope "/subscriptions/$SUB_ID/resourceGroups/expensetracker019-rg"

# Trust pushes to main from your repository
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<your-github-user>/<your-repo>:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

Add a second federated credential with `"subject": "repo:<user>/<repo>:environment:production"` — the workflow's `deploy` job runs in the `production` environment, and the token subject reflects that.

### 3. Repository configuration

**Variables** (Settings → Secrets and variables → Actions → Variables):

| Name | Value |
|---|---|
| `AZURE_RESOURCE_GROUP` | `expensetracker019-rg` |
| `AZURE_NAME_PREFIX` | `expensetracker019` — used in hostnames, so pick something free |

**Secrets:**

| Name | Value |
|---|---|
| `AZURE_CLIENT_ID` | the `$APP_ID` above |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
| `SQL_ADMIN_PASSWORD` | a strong password you generate |
| `ADMIN_SEED_EMAIL` | the app administrator to seed |
| `ADMIN_SEED_PASSWORD` | its password |
| `OPENIDDICT_CERT_BASE64` | see below — may be left empty at first |
| `OPENIDDICT_CERT_PASSWORD` | the PFX password |

Create a `production` environment (Settings → Environments) so the deploy job can require approval if you want one.

### 4. The signing certificate

OpenIddict signs every token. **Without a real certificate the app falls back to ephemeral in-memory keys, and every restart signs everyone out** — on F1, which sleeps, that is often. Fine for a first smoke test, not for users.

```bash
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 3650 -nodes \
  -subj "/CN=expensetracker019"
openssl pkcs12 -export -out signing.pfx -inkey key.pem -in cert.pem -passout pass:<choose-one>
base64 -w0 signing.pfx    # paste into OPENIDDICT_CERT_BASE64
```

A self-signed certificate is correct here: the token signature is verified against Auth019's own JWKS, not a public chain. Set a calendar reminder for the expiry.

---

## Deploying

Merge to `main`. The workflow runs tests, deploys infrastructure, publishes both APIs, builds the SPA against the real hostnames, uploads it, then smoke-tests: it waits for Auth019's discovery document and checks that the API returns 401 to an anonymous call.

To deploy by hand: Actions → Deploy to Azure → Run workflow.

Both databases migrate themselves on startup, as they do locally.

---

## Known gaps

- **.NET 10 on App Service** rolls out per region. If a deploy fails on `DOTNETCORE|10.0`, publish self-contained instead — add `--self-contained -r linux-x64` to the publish steps and set `linuxFxVersion` to `DOTNETCORE|8.0`, which only supplies the host.
- **Google sign-in** needs its redirect URI updated to `https://<auth-app>.azurewebsites.net/signin-google` and the credentials added as app settings.
- **No custom domain.** F1 does not support custom domains with TLS; Static Web Apps Free does, so the SPA can have one even while the APIs stay on `*.azurewebsites.net`.
- **No staging slot.** Deployment slots start at Standard. Merges go straight to production.
- **First request is slow** after idling, on both the apps and the auto-paused database.
