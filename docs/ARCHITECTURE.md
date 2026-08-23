# Architecture

How the system is built and — more importantly — **why**. The rules below are load-bearing: changing them casually breaks the product's guarantees.

---

## Shape of the system

```
                    ┌──────────────────────────┐
                    │  Auth019                 │
   ┌── redirect ───►│  OAuth2 / OIDC server    │──► Auth019Db
   │   (sign in)    │  users · roles · tokens  │    (Identity + OpenIddict)
   │                └──────────────────────────┘
   │                      ▲            ▲
┌──┴──────────────┐       │ JWKS       │ token exchange
│  React SPA      │  code+PKCE         │ (impersonation)
│  (browser)      │───────┘            │
└─────────────────┘                    │
   │  Bearer token                     │
   ▼                                   │
┌──────────────────────────┐           │
│  ExpenseTracker019.Api   │───────────┘
│  resource server only    │──► ExpenseDb (no user table)
└──────────────────────────┘
```

Three deployable pieces, orchestrated in development by **.NET Aspire** (`ExpenseTracker019.AppHost`), which also runs SQL Server in a container and wires the connection strings and service URLs.

### Why authentication is a separate service

Auth019 is the single owner of identity. The expense API has **no user table, no password handling, and issues no tokens** — it only validates what Auth019 signed. That means:

- User data has exactly one home, so there is no sync problem between services.
- The API's attack surface shrinks to "validate a signature and check scopes".
- Any future service (a mobile app, a reporting service) authenticates the same way without duplicating identity code.

The two databases are genuinely separate. The API references users only by the `UserId` taken from a token's `sub` claim, with **no foreign key** to a users table — that FK cannot exist across a service boundary, and pretending otherwise is what turns "separate services" into a distributed monolith.

### Why the SPA uses Authorization Code + PKCE

The browser app is a **public client**: it cannot keep a secret. So it holds no client secret, and PKCE is what proves a code redemption came from the same app that started the flow.

The SPA **never sees a password**. Sign-in happens on Auth019's own server-rendered pages; the SPA only ever receives tokens. This is also why the login/register pages live in `Auth019/Pages/Account/` rather than in React.

Tokens are kept in `sessionStorage` (not `localStorage`) so they die with the tab.

---

## Domain model

```
Category                (soft-deletable, renameable)
└── Head                (soft-deletable, renameable)
    └── Expense         ← every expense hangs off a Head, never a bare Category

BudgetPeriod            (one user's concrete "month": start + end dates)
├── CategoryBudget      (period × category → amount)
└── HeadBudget          (period × head → amount)

UserMonthCycleSetting   (append-only history of cycle start days)
```

A **Category** is a grouping ("Food"). A **Head** is what you actually spend on ("Groceries"). Budgets exist at both levels; spending only happens at head level.

---

## The five rules that matter

### 1. Head budgets can never exceed their category's budget

The core business rule. Enforced in `BudgetService`, **not** as a database constraint — it's a cross-row `SUM` comparison, which a `CHECK` constraint can't express without triggers.

- A **category budget must exist first**. Setting a head budget with no category budget is rejected.
- Setting or raising a head budget sums the *other* heads for that period and rejects if the total would exceed the category budget, naming how much room is actually left.
- **Lowering a category budget below its heads' total is rejected** rather than allowing a temporarily-invalid state.
- Clearing a category's budget for a month **also clears its heads' budgets for that month** — head budgets only exist within a category budget's bounds.

Each check-then-write runs in a transaction so a double-submit can't slip past.

### 2. Categories and Heads are archived, never deleted

The requirement: *"once i already have a category or head set it can [be] removed without removing all its relevant data."*

"Remove" sets `IsArchived = true`. EF Core **global query filters** hide archived rows from ordinary queries.

The consequence to understand: **history and report queries must call `IgnoreQueryFilters()`**, or an expense whose head was archived would silently vanish from the very history it's meant to preserve. `ExpenseService` and `ReportService` both do this deliberately.

Related behaviours: archiving a category cascades to its heads; new expenses cannot target an archived head; an archived name becomes free to reuse; reports include archived items only when they hold that period's spending or budget. Expenses themselves are hard-deleted — correcting a mistyped expense is not a loss of history.

### 3. A "month" is a stored row, not calendar math

Each user picks the day their month starts — 1 for calendar months, 25 for salary-to-salary. Only the start day is stored; the end is always "the day before the next start."

`MonthCycleMath` (pure, unit-tested) handles the edges: a start day of 31 **clamps to the last day** in shorter months, and periods stay contiguous across year boundaries.

Resolved periods are persisted as `BudgetPeriod` rows because budgets need a stable foreign key, and because a later cycle change must not shift periods that budgets already hang off. Reinforcing that: **an existing period covering a date always wins**, and cycle settings are stored append-only rather than overwritten.

### 4. Impersonation is read-only, and enforced as an ordinary scope check

An admin can view a user's account for support but **never act as them**. The mechanism is standard OAuth, not a bespoke side-channel:

- **Scopes are split**: `expense.read` and `expense.write`.
- An admin exchanges their own token via **RFC 8693 token exchange** (`TokenExchangeHandler`) for a token whose subject is the target user and whose scope is **`expense.read` only**, with an `imp_by` claim naming the acting admin.
- That token carries **no roles** and **no refresh token** — it expires in 15 minutes and cannot be silently extended.
- The resource server enforces it through `RequireWriteScopeFilter`, a **global** filter requiring `expense.write` on every non-GET request. Applied globally rather than per-action so an endpoint added later is protected by default.

The elegance here is that the expense API needs no concept of impersonation at all — it just checks a scope. Additional guards in `TokenExchangeHandler`: an admin cannot impersonate themselves, another admin, or a deactivated user, and an already-impersonated session cannot chain another exchange.

### 5. Deactivation is checked at every token-issuing path

`IsActive = false` blocks the **authorize** endpoint, the **refresh_token** grant, and **token exchange**, and `AdminService` revokes the user's stored tokens.

Note the honest limit: access tokens are self-contained JWTs, so an already-issued one stays valid until it expires (15 minutes). Revocation covers refresh tokens and authorization codes; closing the access-token window entirely would require reference tokens and a validation round-trip per request.

---

## Authorization model

Roles come from ASP.NET Identity (`User`, `Admin`) and travel as `role` claims in the access token. The **first admin is seeded from configuration** (`AdminSeed:Email`), so no admin identity is committed to the repo.

**Tenant isolation** is the boundary that matters: every expense-API query is scoped by the user id from the token's `sub` claim via `ICurrentUser`. A client-supplied user id is never trusted. Soft-delete query filters are a *convenience*, not a security boundary.

Auth019's own admin API additionally requires the `auth.admin` scope **and** the Admin role, so an impersonation token (which has neither) cannot reach it.

---

## Aspire orchestration

`AppHost.cs` declares the topology: one SQL Server container with two databases, both services, and the frontend, with `WaitFor` dependencies so nothing starts before what it needs.

Two details worth knowing, both of which caused real bugs:

**Everyone must agree on one issuer string.** Left alone, Auth019 derives `iss` from the request host it sees (Aspire's proxy), while the API is told a different internal address — and validation fails on the mismatch. `AppHost` therefore pins one `authIssuer` value and passes it to both.

**Vite must honour Aspire's `PORT`.** Aspire's proxy forwards to a specific port; Vite otherwise picks its own and drifts to the next free one, leaving the proxy pointing at nothing. `vite.config.ts` reads `process.env.PORT` with `strictPort: true` so a clash fails loudly.

**Deployment**: Aspire is a development-time orchestrator. For production it publishes container artifacts (Azure Container Apps, or docker-compose via Aspirate) — the AppHost itself does not run in production.

---

## Error handling

Domain errors derive from `AppException` with a status code (400/401/403/404/409); middleware maps them to RFC 7807 problem details. Anything else becomes a logged 500 with no internals leaked. In Auth019 the middleware is scoped to `/api` only, so the OAuth endpoints return protocol-correct OAuth errors instead.

Error messages are written for end users and say what to do next — e.g. *"That would put this category's heads at 1000.01, over its 1000 budget. At most 400 is left for this head."* Keep that standard.

---

## Frontend structure

- `auth/oidc.ts` — the `UserManager` (PKCE config) plus the impersonation-token stash
- `auth/AuthContext.tsx` — derives the current user from the access token **actually being sent**, so an impersonated session reports no roles and admin UI cannot appear
- `auth/jwt.ts` — decodes tokens **for display only**; never a security decision
- `api/client.ts` — attaches the right bearer token (impersonation token wins) and throws typed `ApiError`
- `features/` — one folder per screen area; `layouts/AppLayout` gives bottom-tab nav on mobile, top nav on desktop

Server state lives in TanStack Query; mutations invalidate query keys rather than hand-patching caches.

---

## Things that will bite you

**Adding a non-nullable column with a C# default.** EF backfills existing rows with the *SQL type default*, not your initializer. `IsActive = true` once generated `defaultValue: false` and deactivated every account. Always add a matching `HasDefaultValue(...)`, and read generated migrations before applying them.

**OpenIddict requires HTTPS.** `DisableTransportSecurityRequirement()` is set **in Development only** so Aspire can wire things over plain HTTP. Never relax it elsewhere.

**Token exchange reads the subject token from the request body**, not an `Authorization` header — so `TokenExchangeHandler` authenticates with the *server* scheme, not the validation scheme.

**Query filters and eager loading.** An `Include` of a filtered entity silently drops archived rows.

**Testing on Windows.** Use PowerShell `Invoke-RestMethod`, not `curl` — `curl` mangles JSON bodies here. And `sqlcmd` needs `-I` (quoted identifiers) to `UPDATE` tables that carry filtered indexes, or the statement fails while looking like it worked.
