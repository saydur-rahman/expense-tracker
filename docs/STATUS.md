# Project Status

**Last updated:** 2026-08-23

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

**Do this first:** run the AppHost, open http://localhost:5173, and walk through sign-in → month cycle → categories → budgets → expense → dashboard, then the admin screen and "View as". Check a phone viewport too.

### 2. Google sign-in is unproven
Auth019 wires Google as an Identity external provider and the login page shows the button when configured, but no real credentials were ever set, so the path has never executed. Set `Google:ClientId` and `Google:ClientSecret` in Auth019's user-secrets to enable it. Email/password is fully working and independent.

### 3. Production OAuth signing keys are not set up
Development uses OpenIddict's ephemeral dev certificates. Production reads `OpenIddict:SigningCertificateThumbprint` / `OpenIddict:EncryptionCertificateThumbprint` from configuration — **this path has never been run.** Needs real certificates before any deployment.

### 4. Mobile polish pass not done
Layouts are mobile-first with a bottom tab bar, but no dedicated pass happened. Loading and error states are minimal.

### 5. Test coverage is narrow
Only `MonthCycleMath` has unit tests. The budget constraint and the impersonation/scope rules are verified by scripted live calls, not automated tests. Closing that gap is the highest-value testing work: `BudgetService` is pure service logic and easy to test.

### 6. `seed-data` skill is a stub
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
- Aspire allocates service ports dynamically; only the frontend is pinned (5173). Use the Aspire dashboard to find the rest.
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
