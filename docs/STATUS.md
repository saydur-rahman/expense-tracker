# Project Status

**Last updated:** 2026-08-29

Read this first when picking the project back up. It records what is actually built and verified, what is not, and what to do next.

---

## Summary

The app was re-architected from a single API with embedded auth into **two independent services behind an OAuth 2.0 server**, orchestrated by .NET Aspire, on .NET 10.

Everything **builds, runs under Aspire, and has been verified end to end against live services** — including the full Authorization Code + PKCE flow, cross-service token validation, and every security boundary.

The main caveat is unchanged: **the UI has never been opened in a browser.**

---

## Architecture at a glance

| Piece | State |
|---|---|
| `Auth019` — OAuth2/OIDC server (OpenIddict 7), owns users | ✅ Working |
| `ExpenseTracker019.Api` — resource server, owns expense data | ✅ Working |
| `frontend` — React SPA, PKCE redirect flow | ⚠️ Builds & typechecks; **not browser-tested** |
| `ExpenseTracker019.AppHost` — Aspire orchestration | ✅ Working |
| Two separate databases, auto-migrated on startup | ✅ Working |

---

## What's verified

All of the following were exercised against **live running services**, not just compiled:

**OAuth 2.0 / OIDC**
- Discovery document and JWKS served correctly
- Full Authorization Code + PKCE flow: login → code → token exchange, returning access + refresh + id tokens
- Access token carries correct `sub`, `email`, `role`, `scope`, `aud`, `iss`
- Refresh-token grant works and **rotates** the refresh token (a reused one is rejected)
- The expense API — a *separate service* — validates Auth019-issued tokens via JWKS
- Unauthenticated requests are 401

**Impersonation (RFC 8693 token exchange)**
- Produces a token with `scope: expense.read` only, **no roles**, `imp_by` set, and **no refresh token**
- GET allowed; POST / PUT / DELETE all 403 with a clear message
- Cannot reach Auth019's admin API (403)
- Cannot be chained (an impersonated session cannot exchange again)
- Cannot target: yourself, another admin, or a deactivated user

**Deactivation**
- Blocks the `refresh_token` grant ("This account has been deactivated")
- Blocks the authorize endpoint
- Blocks impersonation of that user
- Admins cannot deactivate themselves
- Reactivation restores access

**Domain rules (regression-tested after the refactor)**
- Salary-cycle month resolution (`25 Jul – 24 Aug 2026`)
- Budget constraint: no category budget → rejected; exact match → allowed; over by 0.01 → rejected; lowering category below head sum → rejected
- Dashboard rollup arithmetic exact, over-budget flags correct
- Soft delete: archived head disappears from active lists but its spend stays in history **and** reports; new expenses against it are rejected
- 11 unit tests pass (month-cycle date math)

**Registration form (added 2026-08-29)**
- Collects **mobile number**, **country**, and a **retyped password** alongside name and email
- Verified against the running service: mismatched passwords, a blank mobile, a malformed mobile, and a forged country code are each rejected with a field-level message
- A valid sign-up stores the mobile in Identity's existing `PhoneNumber` column and the ISO 3166-1 alpha-2 code in the new `Country` column
- The country list (244 entries) is derived from the runtime's globalization data in `Models/Countries.cs`, not a hand-kept list — **do not enable `InvariantGlobalization`** on Auth019 or the dropdown empties
- `AddUserCountry` adds a **nullable** column, so existing accounts keep `NULL` and nothing is backfilled

**Budget carry-forward (added 2026-08-30)**
- A new month is seeded with the most recent earlier month's category *and* head budgets, so figures set once keep applying
- Verified end to end against live services (15 checks, `dotnet test` + a scripted OAuth session): next month inherits; editing next month sticks and leaves this month alone; the month after inherits the *edited* figures; a month cleared on purpose stays empty across re-reads and re-resolves; past months are never back-filled; an archived category is not carried forward
- `BudgetsInitialized` on `BudgetPeriod` is what makes it run once per period — set on seeding **and** on every budget write

**Dashboard totals (added 2026-08-30)**
- A total-budget bar (spent vs left) and a Spent/Left donut with the remainder in green, above the per-category cards
- Colours were run through the data-viz validator against both card surfaces: `#6366f1` spent / `#16a34a` left pass the lightness band, chroma floor, colour-blind separation and 3:1 contrast in light **and** dark, so one pair serves both themes
- Over-budget flips both to red and the centre reads "Over"; a month with no budget shows a prompt instead of an empty ring
- The three old Budget/Spent/Left tiles were folded into this block rather than repeating the same figures twice

**Income ledger (added 2026-08-30)**
- `Category.Kind` splits categories into `Expense` and `Income` trees of the same shape; heads inherit the kind. Income never takes a budget
- New `Income` entity, `IncomeService`, `/api/incomes`, an **Income** page, and an Expense/Income switch on the Categories screen
- Dashboard gained an **Income vs expense** donut with the saved figure called out underneath, and **Expense/Income tabs** under it that switch the category breakdown
- Verified end to end against live services (23 checks): the two trees stay separate; the same name is allowed once per ledger; budgets on an income category or head are rejected; spending against an income head and income against a spending head are both rejected; totals (`totalIncome`, `totalSaved`) are exact and go negative when you outspend your income
- Carry-forward (15 checks) and the 11 unit tests re-run clean afterwards

**Currency and the settings screen (added 2026-08-30)**
- Currency is derived from the user's country and carried on the access token as `currency`; every amount in the app — dashboard, expenses, income, budgets — is formatted through one `useMoney()` hook
- New settings shell at `/settings` (sections down the left, content right; pills above the content on a phone), reached by clicking your name in the header. Sections: **Profile** and **Month cycle** (the old `/settings/month-cycle` URL still works)
- Profile edits name, mobile and country; **email is deliberately read-only**. Changing country triggers a silent token renew so the new currency applies immediately
- New `GET/PUT {auth}/api/profile` and `GET {auth}/api/profile/countries` on Auth019
- Verified against live services (21 checks): claims carry BD→BDT; the profile round-trips; an unknown country is rejected; a fresh token reflects a country change to US→USD; an impersonated session may read but **cannot** write (403), and the record is untouched afterwards; anonymous access is 401
- Accounts with no country (everyone created before 29 Aug, including `admin@example.com`) show plain numbers until a country is set on the Profile screen

**Profile completion is enforced before a token is issued (added 2026-08-30)**
- An external sign-up (Google) never sees the registration form, so it arrives with no mobile number and no country — and therefore no currency. New `Pages/Account/CompleteProfile` collects them
- Enforced in `AuthorizationController.Authorize()` rather than the external-login callback, so **holding a session cookie is not a way around it**: any account still missing a country is redirected there before a token is minted, then returned to the interrupted authorize request
- This also catches accounts created **before** the country field existed. `admin@example.com` and `victim@example.com` were given `BD` locally so the test scripts keep working; a real user is simply asked once
- Verified against live services (17 checks): authorize redirects and issues no code; the page renders 244 countries; a blank mobile and a forged country code are both rejected; completing it resumes the original authorize request and the resulting token carries `country` and `currency`; an already-complete account is not interrupted
- Regressions all clean afterwards: profile 21, income 23, month cycle 14, carry-forward 15, unit tests 11

**Logout actually ends the session (fixed 2026-08-30)**
Three separate defects, found by reproducing the reported "it keeps coming back as the previous session":
- **The landing page was protected.** `post_logout_redirect_uri` was `/`, which sits inside `ProtectedRoute` — so signing out immediately started a *new* sign-in. New public `/signed-out` page; the URI is registered in `AuthSeeder` alongside the old one
- **Google silently re-authenticated**, which is why it came back as the *same* account. This turned out to be a symptom of the point above, not a defect: `prompt=select_account` was tried as a mitigation and then **removed** on 2026-08-30 — someone already signed in to Google should go straight through, and Google shows its own chooser when several accounts are signed in. Don't re-add it
- **The refresh token survived logout** — a real security bug, not a symptom. Dropping the cookie left every issued refresh token redeemable, so anything holding one could keep minting access tokens after the user logged out. `Logout()` now revokes the user's tokens via `IOpenIddictTokenManager`, the same way deactivation does
- Verified against live services: the cookie is cleared, the next authorize demands a fresh login, the refresh token is rejected, `prompt=select_account` is sent, and `/signed-out` renders
- **Note:** logout now revokes that user's tokens everywhere, so it signs them out on their other devices too

**Feedback with admin triage (added 2026-08-30)**
- Users send feedback from **Settings → Feedback** and see the whole conversation there; admins work it from **Admin → Feedback** with an Open / In progress / Resolved filter
- A thread is a list of messages, opening message included, so one component renders it for both sides
- Statuses: `Open` → `InProgress` (set automatically when an admin first replies) → `Resolved`. **Resolved closes it to everyone**, enforced in `FeedbackService` so neither controller can bypass it; an admin can reopen by setting the status back
- Admin endpoints require the **Admin role**, which impersonation tokens never carry
- The submitter's name and email are snapshotted onto the row — a deliberate denormalisation, since this service owns no user table and can't join across the service boundary
- Verified against live services (24 checks): submit, list, ownership isolation (another user gets 404), non-admin gets 403, admin reply auto-advances the status, both sides' replies are rejected once resolved and the thread length is unchanged, reopening restores replies, and the status filter works
- Regressions clean: income 23, carry-forward 15, month cycle 14, complete-profile 17, profile 21, unit tests 11

**Month cycle now re-cuts on the spot (fixed 2026-08-30)**
- Changing the cycle start day used to do nothing until the next month: resolution preferred any stored period already covering today. Boundaries are now always computed from the current setting, with the stored row used only as the budget anchor
- Verified against live services (14 checks): 1st → 25th → 15th each re-cut the current month immediately; the dashboard summary follows; previous/next step by the new cycle; and going **back** to the original start day returns the original period row with its 9,000 budget intact
- Carry-forward (15) and income (23) suites re-run clean afterwards

**Dashboard reshaped (2026-08-30)**
- Expense/Income tabs moved to the **top** and now switch the whole view — the Expense tab shows the budget bar and Spent/Left donut with the spending breakdown; the Income tab shows total income, the Income vs Expense donut, the saved figure and the income breakdown
- The donut is thinner (10px stroke, was 16) with an **open centre** — the figures were colliding in the hole, and they already appear in the legend beside it

**Design pass — red / green / blue (2026-08-30)**
- Three colours now carry meaning everywhere: **blue** = brand (actions, links, selection), **green** = money you still have (budget left, income, savings), **red** = trouble (over budget, overspent, destructive actions). Indigo is gone
- Tokens live in `src/frontend/src/index.css` under Tailwind v4's `@theme` as `brand-*`, `positive-*`, `negative-*` — use those, not raw `indigo-*`/`red-*`/`green-*`
- Shared pieces: `components/Button.tsx` (primary / secondary / ghost / danger × sm / md / lg) and `components/ui.ts` (card, emptyState, field, fieldSm, eyebrow, pageTitle)
- Chrome reworked: sticky blurred header with a brand mark, underline nav on desktop, a top-bar marker on the mobile tab bar, softer `rounded-xl` cards with `shadow-sm`, consistent focus rings, and Auth019's sign-in/register pages moved onto the same palette
- The chart mark changed from indigo `#6366f1` to brand blue `#2563eb`; **re-validated with the data-viz validator** against both card surfaces — blue/green passes lightness band, chroma, colour-blind separation and 3:1 contrast in light and dark
- The impersonation banner is now solid red rather than amber: it is deliberately the loudest thing on screen and stays inside the three-colour scheme

**Tinted surfaces, collapsible categories, searchable pickers (2026-08-30)**
- Nothing is pure white or black any more. Surface roles (`page`, `card`, `raised`, `input`, `track`, `line`, `line-soft`, `ink`, `ink-soft`, `ink-muted`) are declared as CSS custom properties and mapped through Tailwind v4 `@theme inline`, so `bg-card` / `text-ink` / `border-line` resolve per theme **without any `dark:` variant** — roughly 200 of them were deleted
- Ground is a soft blue-grey (`#eef2f7` light, `#0b1220` dark) with panels just above it; Auth019's pages use the same values
- Chart marks **re-validated** against the new card surfaces (`#f8fafc` / `#141d2e`) — blue/green still passes every check in both modes
- Dashboard category cards are **collapsible**, showing name, total and progress collapsed, heads when open. They start open when there are ≤ 4 categories and collapsed beyond that
- New `components/SearchableSelect.tsx` — a type-to-filter combobox (keyboard driven, matches on head *or* category name) replacing the native head pickers on Expenses and Income, for both the entry form and the list filter
- Budgets and Categories gained a search box that filters by category or head name, appearing once there are more than 4 categories

**Azure deployment, free tier (added 2026-08-30)**
- `infra/main.bicep` — one free Azure SQL database, an F1 Free Linux App Service plan carrying both APIs, and a Free Static Web App for the SPA. **Compiles clean** (`az bicep build`), 7 resources, 7 outputs
- `.github/workflows/deploy.yml` — merge to `main` runs tests, deploys infrastructure, publishes both APIs, builds the SPA against the real hostnames, uploads it, and smoke-tests (waits for the discovery document; asserts the API 401s anonymously). Signs in with federated OIDC, so no Azure client secret is stored
- **Azure allows one free SQL database per subscription**, so both services share it: Auth019 moved to the `auth` schema (migration `MoveAuthToOwnSchema`) with its own `auth.__EFMigrationsHistory`; the expense API keeps `dbo`. They still share no tables. Splitting later is a connection-string change, not a code change
- Auth019 can now load its OpenIddict signing certificate from a **base64 PFX app setting** — free hosting has no certificate store. With none configured it falls back to ephemeral keys and warns loudly: tokens then die on every restart
- Full setup — resource group, federated credentials, repo secrets, certificate generation — is in [DEPLOY.md](DEPLOY.md)
- **Never deployed.** The template compiles and the workflow parses, but neither has been run against a real subscription

**Aspire orchestration**
- All six resources reach Running: `sql`, `auth019db`, `expensedb`, `auth019`, `expenseapi`, `web`
- Dependency ordering honoured (`Waiting → Running`)
- Frontend served through Aspire's proxy at :5173
- Full PKCE flow + impersonation verified *through the orchestrated stack*, not just standalone

---

## Known gaps

Ordered by how much they should worry you.

### 1. The UI has never been rendered in a browser
Everything frontend-side is verified by TypeScript compilation, a clean production build, and the fact that every API it calls is individually tested — **not** by looking at it. The OAuth redirect flow in particular (`/callback`, silent renew, the impersonation banner) has only been exercised at the protocol level via scripts.

**This now also covers the dashboard donuts, total bar, ledger tabs, the Income page, and the settings/profile screens (added 2026-08-30).** Their arithmetic is verified and the palette was validated by script, but the geometry — arc gaps, the centred figure, how two donuts plus tabs stack on a phone — has never been looked at. Check the empty-budget, part-spent, fully-spent and over-budget states, the no-income state, and the now five-item bottom nav on a narrow screen.

**Do this first:** run the AppHost, open http://localhost:5173, and walk through sign-in → month cycle → categories → budgets → expense → dashboard, then the admin screen and "View as". Check a phone viewport too.

### 2. Google sign-in is unproven
Auth019 wires Google as an Identity external provider and the login page shows the button when configured, but no real credentials were ever set, so the path has never executed. Set `Google:ClientId` and `Google:ClientSecret` in Auth019's user-secrets to enable it. Email/password is fully working and independent.

### 3. Production OAuth signing keys are not set up
Development uses OpenIddict's ephemeral dev certificates. Production reads `OpenIddict:SigningCertificateThumbprint` / `OpenIddict:EncryptionCertificateThumbprint` from configuration — **this path has never been run.** Needs real certificates before any deployment.

### 4. Mobile polish pass not done
Layouts are mobile-first with a bottom tab bar, but no dedicated pass happened. Loading and error states are minimal.

### 5. Test coverage is narrow
Only `MonthCycleMath` has unit tests. The budget constraint and the impersonation/scope rules are verified by scripted live calls, not automated tests. Closing that gap is the highest-value testing work: `BudgetService` is pure service logic and easy to test.

### 6. Nothing has actually been deployed
`infra/main.bicep` compiles and `deploy.yml` parses, but no subscription has run either. Expect the first deploy to surface something — the likeliest candidates are `.NET 10` not yet being available on App Service in your region (fall back to a self-contained publish, noted in DEPLOY.md) and the federated-credential subject not matching. The free SQL database also has to be the *only* free one in the subscription.

### 7. Mobile number and country are collected but never shown
Registration stores both, but nothing surfaces them afterwards — `AdminUserDto` doesn't carry them, there's no profile screen, and no one can correct a typo in their own number. Neither field is validated beyond shape: the mobile is not uniqueness-checked and not verified by SMS.

### 8. `seed-data` skill is a stub
No seeding command exists yet; test data means clicking through the UI or calling the API.

---

## Suggested next steps

1. **Browser-test the whole flow** (gap 1) — highest value, cheapest.
2. **Add unit tests for `BudgetService`** covering the constraint edge cases (gap 5).
3. **Mobile polish pass** (gap 4).
4. **Wire up Google sign-in** when you have credentials (gap 2).
5. **Deployment**: the chosen path is container hosting (Azure Container Apps, or docker-compose on a VPS) — Aspire publishes the manifests. Before that: real signing certificates (gap 3), production connection strings, `Spa:Origin`/CORS set to the real frontend URL, and the SPA client's redirect URIs updated in `AuthSeeder`.

---

## Environment notes

- **Docker Desktop must be running** — Aspire starts SQL Server in a container.
- Service ports are stable, taken from each project's `launchSettings.json`: Auth019 on **5068**, the expense API on **5089**, the frontend on **5173**. Aspire's own dashboard port varies — read it from the AppHost console. (Auth019's fixed port is what makes an OAuth redirect URI like `http://localhost:5068/signin-google` safe to register.)
- Secrets live in .NET user-secrets, never the repo. Auth019: `AdminSeed:Email`, `AdminSeed:Password`, optionally `Google:ClientId`/`Google:ClientSecret`.
- Local dev accounts (throwaway): `admin@example.com` / `Password123!` (Admin), `victim@example.com` / `Password123!` (plain user).
- On Windows use PowerShell `Invoke-RestMethod` rather than `curl` for API testing. `sqlcmd` needs `-I` to `UPDATE` tables with filtered indexes — without it the statement fails while appearing to succeed.

---

## Bugs found and fixed during the build

Recorded because they are the kind that recur.

**Migration would have locked out every existing user.** Adding the non-nullable `IsActive` column backfilled existing rows with SQL's `false` default rather than the C# `= true` initializer. Fixed with `HasDefaultValue(true)`. **Any future non-nullable column with a C# default needs the same treatment.**

**`/me` returned 404 for valid tokens** (pre-OAuth architecture). ASP.NET Core remaps `sub` to a long `ClaimTypes.*` URI by default.

**Token exchange rejected every request.** The handler authenticated with the *validation* scheme, which looks for an `Authorization` header — but a token-exchange request carries the subject token in the form body. Fixed by authenticating with the *server* scheme, which exposes the principal OpenIddict already resolved.

**Token exchange registered as a custom flow.** OpenIddict 7 supports RFC 8693 natively, so `AllowCustomFlow(...)` throws; `AllowTokenExchangeFlow()` is correct.

**Issuer mismatch under Aspire.** Auth019 derived `iss` from the proxy host while the API was configured with a different internal address, so every cross-service call was 401. Fixed by pinning one issuer in `AppHost` and passing it to both.

**Vite ignored Aspire's allocated port**, drifting to 5174 while the proxy pointed at 5173, so the frontend appeared to hang. Fixed with `port: process.env.PORT` + `strictPort`.
