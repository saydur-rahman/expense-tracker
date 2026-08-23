# Project Status

**Last updated:** 2026-08-23

Read this first when picking the project back up. It records what is actually built and verified, what is not, and what to do next.

---

## Summary

The application is **feature-complete against the original spec and works end to end**. Every milestone in [PLAN.md](PLAN.md) is done except the mobile polish pass. 26 API endpoints, 8 screens, 2 migrations applied, committed as `ce8bbaf`.

The main caveat: **the UI has never been opened in a browser.** See [Known gaps](#known-gaps).

---

## What's done

| Area | State | How it was verified |
|---|---|---|
| Project scaffolding | ✅ Done | Both apps build and run |
| Auth — email/password | ✅ Done | Live API: register, login, `/me`, refresh, plus 401/403/409 error paths |
| Auth — Google sign-in | ⚠️ Code complete, **untested** | Needs a real Google OAuth client ID |
| Month cycle + periods | ✅ Done | 11 unit tests + live API; salary cycles, short months, year wrap |
| Categories & Heads | ✅ Done | Live API: create, rename, archive, name reuse, history preservation |
| Budgets + core constraint | ✅ Done | Live API: all four constraint edge cases |
| Expenses | ✅ Done | Live API: create, filter, delete, archived-head history |
| Dashboard / reports | ✅ Done | Live API: rollup math confirmed exact |
| Roles & Admin | ✅ Done | Live API: all security boundaries (see below) |
| Mobile polish pass | ❌ **Not started** | — |

### Security boundaries explicitly verified

These were tested against a running API, not just written:

- A non-admin calling `/api/admin/*` → 403
- Deactivating a user blocks their next login **and** their refresh-token exchange (existing refresh tokens are revoked)
- An impersonation token can `GET` the target's data but is 403 on every `POST`/`PUT`/`DELETE`
- An impersonation token is 403 on `/api/admin/*`
- An admin cannot impersonate another admin, cannot impersonate themselves, and cannot deactivate themselves
- An impersonated session reports no roles, so it cannot inherit admin rights

---

## Known gaps

Ordered by how much they should worry you.

### 1. The UI has never been rendered in a browser
Everything frontend-side is verified by TypeScript compilation, a clean production build, and the fact that the APIs it calls are individually tested — **not** by looking at it. Layout bugs, broken interactions, and styling problems would not have been caught.

**Do this first:** run both servers, click through register → set month cycle → add a category and heads → set budgets → log an expense → check the dashboard. Also check it at a phone viewport width.

### 2. Google sign-in is unproven
The backend verifies Google ID tokens (`Google.Apis.Auth`) and the frontend renders a Google button, but no real client ID was ever configured, so the path has never executed. To test: create a Google OAuth **Web application** client ID, then set `Google:ClientId` (backend user-secrets) and `VITE_GOOGLE_CLIENT_ID` (in `frontend/.env.development`).

Email/password auth is fully working and independent of this.

### 3. Mobile polish pass not done (Milestone 7)
Layouts were written mobile-first with a bottom tab bar and touch-sized targets, but no dedicated pass happened. Loading and error states are minimal, and there's no offline or empty-state polish beyond basic messages.

### 4. `seed-data` skill is a stub
`.claude/skills/seed-data/SKILL.md` describes what to build but there is no seeding command yet. Creating test data currently means clicking through the UI or calling the API.

### 5. Test coverage is narrow
Only `MonthCycleMath` has unit tests (11, all passing). The budget constraint — the most important rule in the app — is verified by manual API calls, **not** by automated tests. That's the most valuable gap to close: it's pure service logic and easy to test.

---

## Suggested next steps

1. **Browser-test the whole flow** (gap 1) — highest value, cheapest.
2. **Add unit tests for `BudgetService`** covering the constraint edge cases (gap 5).
3. **Mobile polish pass** (gap 3).
4. **Wire up Google sign-in** when you have a client ID (gap 2).
5. **Deployment to SmarterASP.NET** — deliberately out of scope so far, never attempted. Needs: publish profile, a production connection string, `Jwt:Key` and `AdminSeed:Email` as host config, CORS origins updated for the real frontend URL, and a decision on where the SPA is served from.

---

## Environment notes

- Local DB: `Server=.;Database=ExpenseTrackerDb;Trusted_Connection=True` (in `appsettings.Development.json`)
- Secrets are in .NET user-secrets, not in the repo: `Jwt:Key`, `Google:ClientId`, `AdminSeed:Email`
- The dev database currently holds throwaway test accounts (`test@example.com`, `e2e@example.com`, etc., all password `Password123`). `test@example.com` is the seeded admin. Wipe with the `db-reset` skill whenever you want a clean slate.
- On Windows, use PowerShell's `Invoke-RestMethod` rather than `curl` for API testing — `curl` mangles JSON bodies in this environment.

---

## Bugs found and fixed during the build

Recorded because both are the kind that recur.

**Migration would have locked out every existing user.** Adding the non-nullable `IsActive` column backfilled existing rows with SQL's `false` default rather than the C# `= true` initializer, deactivating every account. Fixed with `HasDefaultValue(true)` in `AppDbContext`. **Any future non-nullable column with a C# default needs the same treatment.**

**`/me` returned 404 for valid tokens.** ASP.NET Core's JWT handler remaps `sub` to a long `ClaimTypes.*` URI by default, so looking up `sub` found nothing. Fixed with `options.MapInboundClaims = false`.
