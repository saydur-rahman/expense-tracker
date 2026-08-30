# Deploying to Azure

Everything here is chosen to cost **nothing**. Read the caveats before you rely on it.

---

## What gets created

| Piece | Azure resource | Tier | Cost |
|---|---|---|---|
| Database (both services) | Azure SQL Database | Free offer — 100,000 vCore-seconds + 32 GB/month | £0 |
| Auth019 + expense API | App Service on Linux | **F1 Free** plan, both apps on it | £0 |
| React SPA | Static Web Apps | Free (TLS included) | £0 |
| CI/CD | GitHub Actions | Free minutes | £0 |

`infra/main.bicep` provisions all of it. `.github/workflows/deploy.yml` builds, deploys and smoke-tests on every merge to `main`.

**Aspire is not deployed.** It orchestrates local development only; in Azure these are plain App Service apps wired together with app settings.

---

## The two constraints that shaped this

**1. Azure gives one free SQL database per subscription.** The app wants two. Both services therefore share a single database under separate schemas — Auth019 owns everything in `auth`, the expense API owns `dbo`. They still share no tables and keep separate migration histories (`auth.__EFMigrationsHistory` and `dbo.__EFMigrationsHistory`). This is a deliberate departure from "each service owns its own database" in [ARCHITECTURE.md](ARCHITECTURE.md), taken to keep the bill at zero.

To split them later: create a second database, point `ConnectionStrings__expensedb` at it, and redeploy. No code change — the schema separation already keeps them apart.

**2. F1 Free is genuinely limited.** 60 CPU-minutes/day, 1 GB RAM shared by both apps, and **no Always On** — the first request after an idle period is slow while the app wakes. Exceed the daily CPU quota and App Service returns `403 Quota exceeded` until the next day. It is fine for yourself and a handful of testers; it is not fine for real traffic. The upgrade is B1 (~£10/month), a one-line `sku` change in the Bicep.

The free SQL database is set to **auto-pause** when the monthly grant runs out rather than bill you. The app stops working until the grant renews on the 1st. Change `sqlFreeLimitExhaustionBehavior` to `BillOverUsage` only when uptime matters more than a zero bill.

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
