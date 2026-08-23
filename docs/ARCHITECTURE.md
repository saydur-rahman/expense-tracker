# Architecture

How the app is built and — more importantly — **why**. The rules below are load-bearing: changing them casually will break the product's guarantees.

---

## Shape of the system

```
┌─────────────────┐         REST + JWT          ┌──────────────────┐
│  React SPA      │ ──────────────────────────► │  ASP.NET Core    │ ──► SQL Server
│  (Vite, TS)     │ ◄────────────────────────── │  Web API (.NET 8)│
└─────────────────┘      JSON, Bearer header    └──────────────────┘
```

The frontend and backend are fully separate. The API knows nothing about the SPA; the SPA talks to it purely over REST with a bearer token.

**Why:** the stated goal is to convert this into a mobile app later. A React SPA over a pure REST API means a React Native client can reuse the same API layer and the same auth, unchanged. This is also why auth uses **JWT in an `Authorization` header rather than cookies** — that works identically inside a WebView or native HTTP client, whereas cookie auth brings CORS and redirect complications.

**Backend is a single project**, organised by folders (`Models/`, `Data/`, `Services/`, `Controllers/`, `Dtos/`), not a layered multi-project solution. The app is small and the user asked to keep it simple; splitting into Domain/Application/Infrastructure libraries would add ceremony without payoff. Revisit only if it genuinely outgrows this.

---

## Domain model

```
ApplicationUser
├── UserMonthCycleSetting  (append-only history of cycle start days)
├── Category               (soft-deletable, renameable)
│   └── Head               (soft-deletable, renameable)
│       └── Expense        ← every expense hangs off a Head, never a bare Category
├── BudgetPeriod           (one user's concrete "month": start + end dates)
│   ├── CategoryBudget     (period × category → amount)
│   └── HeadBudget         (period × head → amount)
└── RefreshToken
```

A **Category** is a grouping ("Food"). A **Head** is what you actually spend on ("Groceries", "Dining out"). Budgets exist at both levels; spending only happens at head level.

---

## The four rules that matter

### 1. Head budgets can never exceed their category's budget

The core business rule. Enforced in `BudgetService`, **not** as a database constraint — it's a cross-row `SUM` comparison, which a `CHECK` constraint can't express without triggers.

Concretely:
- A **category budget must exist first**. Setting a head budget with no category budget is rejected — the whole point is bounding heads against a total.
- Setting or raising a head budget sums the *other* heads in that category for that period and rejects if the new total would exceed the category budget. The error names how much room is actually left.
- **Lowering a category budget below its heads' current total is rejected** rather than allowing a temporarily-invalid state. The invariant holds at all times.
- Clearing a category's budget for a month **also clears its heads' budgets for that month** — head budgets only exist within a category budget's bounds, so leaving them unbounded would violate the rule.

Each check-then-write runs in a transaction so a double-submit can't slip past.

### 2. Categories and Heads are archived, never deleted

The user's requirement: *"once i already have a category or head set it can [be] removed without removing all its relevant data."*

So "remove" sets `IsArchived = true`. EF Core **global query filters** hide archived rows from ordinary queries, so normal screens don't need to remember to filter.

The consequence to understand: **history and report queries must call `IgnoreQueryFilters()`**, otherwise an expense whose head was archived would silently vanish from the very history it's meant to preserve. `ExpenseService` and `ReportService` both do this deliberately. EF will warn at migration time about required relationships with query filters — that warning is expected and is exactly this tradeoff.

Related behaviours:
- Archiving a category cascades to archiving its heads, so the pair stays consistent.
- New expenses **cannot** target an archived head — history is preserved, but the head is closed to new spending.
- An archived name becomes free to reuse.
- Reports include archived items only when they hold that period's spending or budget, so old items don't clutter current views.
- Expenses themselves are hard-deleted; deleting a mistyped expense is a correction, not a loss of history.

### 3. A "month" is a stored row, not calendar math

Each user picks the day their month starts (`UserMonthCycleSetting.StartDay`) — 1 for calendar months, 25 for salary-to-salary. Only the start day is stored; the end is always "the day before the next start."

`MonthCycleMath` (pure, unit-tested) handles the edges: a start day of 31 **clamps to the last day** in shorter months, and periods stay contiguous across year boundaries.

Resolved periods are persisted as `BudgetPeriod` rows rather than recomputed on the fly, for two reasons:
1. Budgets need a stable foreign key to attach to.
2. If a user later changes their cycle, past periods must not silently shift underneath budgets already set against them.

Reinforcing that: **an existing period covering a date always wins.** `MonthCycleService.ResolvePeriodContainingAsync` looks for a stored period first and only computes a new one if none covers the date. Cycle settings are also stored append-only (effective-dated) rather than overwritten.

Expenses store only their date — the period they belong to is resolved at query time by matching against period ranges. Nothing needs reassigning if periods change.

### 4. Impersonation is read-only and can't escalate

An admin can view a user's account for support, but **never act as them**. Layered defences:

- **The token** is short-lived (15 min), carries `imp_readonly=true` and `imp_by=<admin id>`, has **no role claims**, and has **no refresh token** — it expires and cannot be silently extended.
- **`ImpersonationReadOnlyMiddleware`** rejects every non-`GET` request centrally. This is deliberately not per-endpoint: a write endpoint added later is read-only under impersonation **by default**, with no one needing to remember the check.
- **`/api/admin/*` is blocked** during impersonation, closing the privilege round-trip.
- **Admins cannot be impersonated**, so one admin can't borrow another's authority.
- **`/api/auth/me` reports no roles** while impersonating, so the frontend can't be tricked into showing admin UI.

Account **deactivation** is checked at *every* token-issuing path — login, Google login, and refresh — and revokes outstanding refresh tokens. Without the refresh check, a deactivated user holding a refresh token could keep minting access tokens indefinitely.

---

## Authorization model

Two roles via ASP.NET Identity's built-in role system (`User`, `Admin`) rather than a custom "user type" column — that gives role claims in the JWT, `[Authorize(Roles = …)]` on endpoints, and room for more roles without a schema change.

The **first admin is seeded from configuration** (`AdminSeed:Email`): on startup, if a user with that email exists, they're granted the Admin role. Nothing is hardcoded in the repo and it works identically on the host.

**Tenant isolation** is the important boundary: every service scopes its queries by the authenticated user's id, taken from the JWT `sub` claim via `ICurrentUser`. A client-supplied user id is never trusted. Soft-delete query filters are a *convenience*, not a security boundary — the `UserId` scoping is what actually keeps users' data apart.

---

## Error handling

Domain errors derive from `AppException` with a status code (`ValidationAppException` → 400, `UnauthorizedAppException` → 401, `ForbiddenAppException` → 403, `NotFoundAppException` → 404, `ConflictAppException` → 409). `ExceptionHandlingMiddleware` maps them to RFC 7807 problem-details responses; anything else becomes a logged 500 with no internals leaked.

Error messages are written for end users and say what to do next — e.g. *"That would put this category's heads at 1000.01, over its 1000 budget. At most 400 is left for this head."* Keep that standard when adding errors.

---

## Frontend structure

- `api/` — one typed module per resource, over a shared `client.ts` that attaches the bearer token and throws a typed `ApiError`
- `auth/` — `AuthContext` plus route guards: `ProtectedRoute` (signed in), `AdminRoute` (admin role), `RequireMonthCycle` (onboarding)
- `features/` — one folder per screen area
- `layouts/AppLayout` — bottom tab bar on mobile, top nav on desktop

Server state lives in **TanStack Query**; React state is only for forms and UI. Mutations invalidate query keys rather than hand-updating caches.

Impersonation on the client keeps the **admin's own token stashed** under a separate key, so exiting restores it without a re-login. The amber banner is always visible while impersonating.

Mobile-first: base styles target narrow viewports, `md:`/`lg:` breakpoints layer on desktop. The API base URL comes from an env var so a packaged mobile build can point elsewhere.

---

## Things that will bite you

**Adding a non-nullable column with a C# default.** EF backfills existing rows with the *SQL type default*, not your C# initializer. `ApplicationUser.IsActive = true` produced a migration with `defaultValue: false` and deactivated every existing account. Always add `HasDefaultValue(...)` in `AppDbContext` to match. Read generated migrations before applying them.

**JWT claim remapping.** ASP.NET Core rewrites `sub` to a long `ClaimTypes.*` URI unless `MapInboundClaims = false` — which is set in `Program.cs`. Don't remove it; `ICurrentUser` depends on reading `sub` directly.

**Query filters and eager loading.** An `Include` of a filtered entity silently drops archived rows. If a query needs archived data, `IgnoreQueryFilters()` must be on the query — this is why history queries look different from list queries.

**Testing on Windows.** Use PowerShell `Invoke-RestMethod`, not `curl` — `curl` mangles JSON bodies and hides error payloads in this environment.
