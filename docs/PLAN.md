# Expense Tracker — Implementation Plan

> **Historical record.** This is the plan the project was built from, kept for the requirements
> and reasoning it captures. It describes intended scope, not necessarily current state — for
> what actually exists today see [STATUS.md](STATUS.md), and for how the system works see
> [ARCHITECTURE.md](ARCHITECTURE.md). All milestones here are complete except Milestone 7
> (mobile polish).

## Context

A free, simple expense-tracking web app for multiple people, built mobile-first so it can later be wrapped/converted into a native mobile app. It supports each user defining their own **Categories**, each with **Heads** (sub-line-items), monthly **budgets** at both levels with the rule that a category's heads can never budget more in total than the category itself, a **custom personal month cycle** (not necessarily the calendar month), and **expense tracking** against heads. Categories/Heads must be renameable anytime and removable without destroying historical data (soft delete). Users additionally have a **role/user type** governing what they can access, with an **Admin** role that can list/search all users, see last-login times, deactivate accounts, and impersonate a user read-only for support. The project began empty — this was a from-scratch build. Backend is ASP.NET Core (.NET 8) + SQL Server, matching the owner's existing skill set and hosting (SmarterASP.NET, which supports both — deployment itself was out of scope for this plan). Frontend is a React SPA for mobile-friendliness and easy future porting. Auth supports both email/password and Google sign-in. Alongside the app, Claude Code project skills were to be set up so future sessions can run/migrate/seed the project easily.

## Tech Stack

- **Backend**: ASP.NET Core Web API, .NET 8 LTS, EF Core, SQL Server
- **Auth**: ASP.NET Core Identity (email/password) + Google ID-token sign-in, both issuing the app's own JWT (stateless, works identically from a future native/WebView wrapper)
- **Frontend**: React + TypeScript (Vite), React Router, TanStack Query for API data, Tailwind CSS for mobile-first responsive styling, React Hook Form + Zod for forms
- **Project layout**: kept intentionally simple — **one ASP.NET Core Web API project** organized by folders (Models, Data, Services, Controllers, DTOs) rather than a multi-project Domain/Application/Infrastructure split. This is a small app for now; splitting into separate class libraries can happen later if it actually grows enough to need it.

## Data Model (EF Core entities, SQL Server)

- **ApplicationUser** (extends `IdentityUser<Guid>`) — adds `DisplayName`, `CreatedAtUtc`, plus admin-facing fields: `IsActive` (bool, default true), `DeactivatedAtUtc` (nullable), `LastLoginAtUtc` (nullable). Google sign-in uses Identity's built-in external-login linking (`AddLoginAsync`) against the same user table — no separate user type. Auto-link to an existing email/password account only when Google reports the email as verified and it matches exactly.

- **UserMonthCycleSetting** — `UserId` (FK), `StartDay` (int 1–31), `EffectiveFromUtc`, `CreatedAtUtc`. Only a start day is stored; the end of the cycle is always "day before next start," including short-month roll-over (e.g. start day 31 in a 30-day month). Stored as an append-only, effective-dated list so changing your cycle later doesn't retroactively shift already-computed past periods.

- **Category** — `Id`, `UserId` (FK), `Name`, `IsArchived`, `ArchivedAtUtc`, `CreatedAtUtc`, `DisplayOrder`.
- **Head** — `Id`, `CategoryId` (FK), `Name`, `IsArchived`, `ArchivedAtUtc`, `CreatedAtUtc`, `DisplayOrder`.
  - Rename = plain `Name` update.
  - Delete = soft delete (`IsArchived = true`), never a hard delete, so historical budgets/expenses referencing an archived Category/Head stay intact. Archiving a Category cascades to archiving its Heads.
  - EF Core global query filter (`HasQueryFilter(x => !x.IsArchived)`) hides archived items from normal list queries; history/report queries explicitly use `IgnoreQueryFilters()`.
  - FK delete behavior `Restrict` everywhere as a guard against accidental hard deletes at the DB level.

- **BudgetPeriod** — `Id`, `UserId` (FK), `StartDate`, `EndDate` (both `DateOnly`, resolved concrete dates), `Label`. Unique on `(UserId, StartDate)`. This is the explicit representation of a user's "month" — created lazily (on first access to "current"/"next" period) by resolving the user's `StartDay` setting, rather than being computed ad hoc every time, so budgets have a stable thing to attach to and a period's boundaries never silently shift if the user later changes their cycle.

- **CategoryBudget** — `Id`, `BudgetPeriodId` (FK), `CategoryId` (FK), `Amount decimal(18,2)`, timestamps. Unique on `(BudgetPeriodId, CategoryId)`.
- **HeadBudget** — `Id`, `BudgetPeriodId` (FK), `HeadId` (FK), `Amount decimal(18,2)`, timestamps. Unique on `(BudgetPeriodId, HeadId)`.
  - Deleting/resetting a month's budgets = deleting the `CategoryBudget`/`HeadBudget` rows for that one `BudgetPeriodId` — the Category/Head definitions and every other period are untouched.

- **Expense** — `Id`, `UserId` (FK, denormalized for fast/simple authorization checks), `HeadId` (FK, required — every expense belongs to a Head, never a bare Category), `Amount decimal(18,2)` (CHECK > 0), `ExpenseDate DateOnly`, `Note` (nullable, max 500). A plain hard delete is fine here (correcting a mistaken entry isn't "losing history" the way archiving a category would be). An expense's period is computed at query time by matching `ExpenseDate` against the user's `BudgetPeriod` ranges — not stored redundantly.

**The core business rule** — sum of a Category's Head budgets must never exceed that Category's own budget for the same period — is enforced **server-side in a `BudgetService`**, not as a SQL constraint (cross-row SUM checks aren't practical as a plain CHECK constraint):
- Setting/raising a Head budget: sum existing Head budgets under that Category+period (excluding the one being edited) + new amount, reject with a clear 400 error (showing category total, current sum, attempted amount) if it would exceed the Category budget.
- A Category budget must exist before Head budgets can be set under it (unset = no Head budgets allowed yet).
- Lowering a Category budget below the current sum of its Heads' budgets is rejected with a clear error, rather than allowing a temporarily-invalid state.
- Wrapped in a transaction to avoid race conditions from double-submits.

Indexes: `Category.UserId`, `Head.CategoryId`, `Expense(HeadId, ExpenseDate)`, unique indexes noted above. All queries additionally scoped by the authenticated user's `UserId` from claims at the service layer (never trust a client-supplied user id) — this is the real multi-tenant isolation boundary, not just the archive filter.

## Roles & Admin

**Roles use ASP.NET Core Identity's built-in role system** (`IdentityRole<Guid>`, already wired into `AppDbContext`) rather than a custom "user type" column — it gives role claims in the JWT, `[Authorize(Roles = "...")]` on endpoints, and room to add more roles later without a schema change.

- Roles seeded on startup: **`User`** (default for everyone who registers) and **`Admin`**.
- The user's roles are written into the JWT as role claims at token-issue time, so `[Authorize(Roles = "Admin")]` works without a DB hit per request.
- **First admin is seeded from configuration**: an `AdminSeed:Email` value (in user-secrets locally, app settings on the host) — on startup, if a user with that email exists, they're added to the `Admin` role. Nothing hardcoded in the repo, and it works the same on SmarterASP.NET.

**Account deactivation** (`ApplicationUser.IsActive`):
- Login, Google login, and refresh all reject a deactivated user with a clear 403 — checked in `AuthService` at every token-issuing path, so an existing access token can't outlive deactivation for more than its short lifetime.
- On deactivation, all of that user's refresh tokens are revoked so they can't mint new access tokens.
- Deactivation is reversible (reactivate), and never deletes data.

**Last login** (`ApplicationUser.LastLoginAtUtc`) — stamped on every successful password/Google login (not on refresh, so it reflects real sign-ins).

**Impersonation — read-only.** An admin can view a user's data exactly as that user sees it, but every write is blocked:
- `POST /api/admin/users/{id}/impersonate` (Admin only) issues a **short-lived, non-refreshable** access token whose subject is the target user, plus two extra claims: `imp_by` (the admin's own user id, for audit) and `imp_readonly=true`. No refresh token is issued — impersonation expires and cannot be silently extended.
- An `ImpersonationReadOnlyMiddleware` (or an authorization filter) rejects any non-GET request carrying `imp_readonly=true` with a 403. This is enforced centrally rather than per-endpoint, so a future write endpoint is safe by default instead of needing to remember the check.
- Impersonation tokens cannot be minted for another Admin, and an impersonated session can never reach `/api/admin/*` (the admin endpoints additionally require the absence of `imp_readonly`) — this prevents privilege round-trips.
- `GET /api/auth/me` reports `isImpersonating` + `impersonatedBy` so the frontend can show a persistent "You are viewing as <user> (read-only)" banner with an exit button.

## Backend Structure & Key Endpoints

Single `ExpenseTracker.Api` project:
- `Models/` — entities above
- `Data/AppDbContext.cs` — `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` + entity configs, query filters, migrations
- `Services/` — `CategoryService`, `HeadService`, `BudgetService` (houses the sum-constraint rule), `ExpenseService`, `ReportService`, `MonthCycleService`
- `Controllers/` — grouped by resource, all `[Authorize]`
- `DTOs/` — request/response shapes
- `Program.cs` — Identity + JWT bearer auth + Google token verification (`Google.Apis.Auth`) + CORS (for the separately-hosted SPA origin) + Swagger

Endpoints (all scoped to the authenticated user):
- **Auth**: `POST /api/auth/register`, `/login`, `/google` (verifies a Google ID token sent from the frontend, finds/creates user, issues app JWT), `/refresh`, `GET /api/auth/me`
- **Settings**: `GET/PUT /api/settings/month-cycle`
- **Categories**: `GET /api/categories[?includeArchived]`, `POST`, `PUT /{id}` (rename), `DELETE /{id}` (archive)
- **Heads**: `GET/POST /api/categories/{categoryId}/heads`, `PUT /api/heads/{id}`, `DELETE /api/heads/{id}`
- **Budget periods**: `GET /api/budget-periods/current`, `GET /api/budget-periods?relativeMonth=`, `GET /api/budget-periods` (history)
- **Budgets**: `GET /api/budget-periods/{periodId}/budgets` (categories + nested heads + amounts + remaining allowance), `PUT/DELETE .../categories/{categoryId}/budget`, `PUT/DELETE .../heads/{headId}/budget`
- **Expenses**: `GET /api/expenses?from=&to=&categoryId=&headId=&page=`, `POST`, `PUT /{id}`, `DELETE /{id}`
- **Reports**: `GET /api/reports/summary?periodId=` and `/summary/current` — per-category rollup (budget/actual/remaining/% used) with nested per-head breakdown
- **Admin** (all `[Authorize(Roles = "Admin")]`, and all rejected inside an impersonated session):
  - `GET /api/admin/users?search=&page=&pageSize=&includeInactive=` — paginated user list with search over email/display name, returning role, `IsActive`, `LastLoginAtUtc`, `CreatedAtUtc`
  - `GET /api/admin/users/{id}` — single user detail
  - `POST /api/admin/users/{id}/deactivate` / `POST /api/admin/users/{id}/reactivate`
  - `POST /api/admin/users/{id}/impersonate` — returns a short-lived read-only access token for that user

## Frontend Structure (React, mobile-first)

```
frontend/src/
├── api/          # typed client functions per resource
├── auth/         # AuthContext, ProtectedRoute, Google sign-in button
├── components/   # Button, Modal, AmountInput, MonthPicker, BudgetProgressBar, etc.
├── features/
│   ├── categories/   # list/expand/rename/archive
│   ├── budgets/      # BudgetSetupPage — period picker, category+head inputs with live "allocated vs total" feedback
│   ├── expenses/     # quick-add form, filterable history list
│   ├── dashboard/    # budget-vs-actual cards per category/head, progress bars
│   ├── admin/        # user list + search, deactivate/reactivate, impersonate
│   └── settings/     # month cycle setup
├── layouts/      # bottom-tab nav on mobile, side-nav on desktop
└── router.tsx
```

Key screens: Login/Register (+ "Continue with Google"), first-login month-cycle onboarding, Categories & Heads management, Monthly Budget Setup, Expense Entry (quick-add, mobile-first as the most frequent action), Expense History (filters), Dashboard (default landing page — budget vs actual with progress bars, over-budget flags), and an **Admin Users** screen (searchable user list with last-login, active/inactive state, deactivate/reactivate, and "View as user").

Admin routing: an `AdminRoute` guard (same shape as `ProtectedRoute`) gates `/admin/*` on the `Admin` role claim; the admin nav entry only renders for admins. While impersonating, a persistent banner sits above the layout showing who's being viewed with an "Exit" action that restores the admin's own stored token — the frontend keeps the admin's real token aside rather than discarding it, so exiting never requires re-login.

Mobile-first: base styles for narrow viewport, `md:`/`lg:` breakpoints added on top; JWT in an `Authorization` header (not cookies) so the same auth works unchanged inside a future WebView/native wrapper; API base URL from an env var.

## Build Order

1. **Scaffolding** — solution + EF Core `AppDbContext` with Identity, initial migration, Vite React skeleton, CORS/Swagger; confirm both run.
2. **Auth** — register/login/JWT issuance, then Google sign-in (reuses the same JWT path).
3. **Month cycle setting** — entity, endpoint, onboarding screen, period-resolution logic — build/verify this early since everything downstream depends on correct period math.
4. **Categories & Heads CRUD** — soft-delete/rename endpoints + management screens (first real end-to-end milestone).
5. **Budgets** — `BudgetPeriod`/`CategoryBudget`/`HeadBudget` + the sum-constraint service (test edge cases: exact match, exceed by 0.01, lowering category below head sum, no category budget set yet) + budget setup screen.
6. **Expenses** — entity/endpoints + entry form + history list.
7. **Dashboard/Reports** — summary rollup service + dashboard screen tying it all together.
8. **Roles & Admin** — role seeding + role claims in the JWT, `IsActive`/`LastLoginAtUtc` fields and the deactivation checks in `AuthService`, admin user list/search/deactivate endpoints, read-only impersonation (token minting + the central write-blocking middleware), and the admin screens + impersonation banner. Built after the core app so there is real user data to administer, and so the read-only middleware is written against the full set of write endpoints it must cover.
9. **Polish** — mobile responsive pass, error/loading states, refresh-token hardening, client/server validation parity.

*(Deployment to SmarterASP.NET is intentionally out of scope for this plan.)*

## Claude Code Project Skills (`.claude/skills/`)

1. **run-dev** — starts backend (`dotnet watch`) and frontend (`npm run dev`) together for local development.
2. **ef-migration** — wraps `dotnet ef migrations add <Name>` / `dotnet ef database update` with the right project flags, so migrations are always created/applied consistently.
3. **seed-data** — seeds a demo user with sample categories, heads, budget periods, budgets, and expenses, for quickly exercising the dashboard without manual data entry.
4. **db-reset** — drops/recreates the local dev DB and re-seeds, for recovering from broken migration history during early schema churn.
5. **api-smoke-test** — hits the key REST endpoints (auth, categories, budgets, expenses, reports) to sanity-check the API without needing the frontend running.

## Verification

After each milestone, run `run-dev` and exercise the flow end-to-end in a browser (including a mobile viewport width): register/login (both email and Google), set a month cycle, create a category with two heads, set a category budget and head budgets (confirm the app rejects a head-budget sum that exceeds the category total), log an expense against a head, and confirm the dashboard shows correct budget-vs-actual numbers for the current period.

For the admin milestone specifically, verify the security boundaries, not just the happy path:
- A non-admin user calling any `/api/admin/*` endpoint gets 403, not data.
- Deactivating a user blocks their next login **and** their refresh-token exchange.
- An impersonation token can read the target user's data (GET) but is rejected on every write (POST/PUT/DELETE) with 403.
- An impersonation token cannot call `/api/admin/*`, and an admin cannot mint one for another admin.
