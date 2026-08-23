# Agent Guide — expensetracker019

Orientation for AI coding assistants. Read this before making changes.
(`CLAUDE.md` points here — this is the single source of truth for both.)

---

## Orient yourself first

| Read | For |
|---|---|
| **[docs/STATUS.md](docs/STATUS.md)** | **Start here.** What's built, what isn't, known gaps, next steps. |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | How the two services fit together and *why*. |
| [docs/API.md](docs/API.md) | Endpoints and OAuth flows. |
| [README.md](README.md) | Setup and running. |
| [docs/PLAN.md](docs/PLAN.md) | Original plan (historical; predates the OAuth split). |

**Docs are current as of 2026-08-23.** If they disagree with the code, the code wins — and please fix the doc.

---

## What this is

A mobile-first expense tracker split into **two independent services**:

- **`src/Auth019`** — OAuth 2.0 / OIDC server (OpenIddict 7). Owns **all** user data, sign-in pages, roles, and user administration. Has its own database.
- **`src/ExpenseTracker019.Api`** — pure resource server for the expense domain. **No user table, no password handling, issues no tokens.** Its own database. Knows users only by the `sub` claim.
- **`src/frontend`** — React SPA. Signs in by redirecting to Auth019 (Authorization Code + PKCE); never handles a password.
- **`src/ExpenseTracker019.AppHost`** — .NET Aspire orchestration (SQL Server container + both services + frontend).

**Stack:** .NET 10, ASP.NET Core, EF Core, SQL Server, OpenIddict 7, Aspire 13; React 19 + TypeScript + Vite + Tailwind v4 + oidc-client-ts + TanStack Query.

---

## Rules you must not break

These encode explicit product and security requirements. **Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before touching any of them.**

1. **The expense API must never grow a user table, password handling, or token issuance.** Auth019 owns identity. There is deliberately no foreign key from expense data to users — that FK cannot cross a service boundary.

2. **A category's head budgets can never exceed the category's own budget** for that period. Lives in `BudgetService`, transactionally. Not a DB constraint (cross-row SUM). A category budget must exist before any head budget; clearing a category's budget for a month clears its heads' budgets too.

3. **Categories and Heads are never hard-deleted.** `IsArchived` + EF global query filters. Their past expenses and budgets must stay visible in history and reports — which is why those queries call `IgnoreQueryFilters()`. **If you add a history or report query, it needs that too**, or archived data silently vanishes from the views meant to preserve it.

4. **A "month" is a stored `BudgetPeriod` row**, not calendar math. Users pick their own cycle start day. An existing period covering a date always wins. Date edge cases live in `MonthCycleMath` (unit-tested — extend those tests if you touch it).

5. **Impersonation is read-only, enforced as a scope check.** The impersonation token carries `expense.read` only, no roles, no refresh token. `RequireWriteScopeFilter` is registered **globally** so any new write endpoint is protected by default — **don't add per-endpoint exceptions**, and don't make the expense API aware of impersonation as a concept.

6. **Deactivation must be checked at every token-issuing path** (authorize, refresh, token exchange).

7. **Every expense-API query is scoped by the `sub` claim** via `ICurrentUser`. Never trust a client-supplied user id. This — not the archive filter — is the tenant isolation boundary.

---

## Conventions

- **Business logic goes in `Services/`**, not controllers. Controllers bind, delegate, return.
- **Errors**: throw `AppException` subclasses (400/403/404/409); middleware maps them. Write messages for end users saying what to do next — match the tone of the budget errors.
- **Frontend server state** goes through TanStack Query; invalidate query keys after mutations.
- **`auth/jwt.ts` decodes tokens for display only** — never a security decision. The API validates every token.
- **Mobile-first CSS**: base styles narrow, `md:`/`lg:` on top.
- **Money** is `decimal(18,2)`, configured explicitly.
- **Package versions** are centralized in `Directory.Packages.props`; don't put `Version=` in a csproj.
- Match surrounding style. Comments explain *why*, not *what*.

---

## Working on this

```bash
# Everything (needs Docker Desktop running)
cd src/ExpenseTracker019.AppHost && dotnet run

# Checks
dotnet test
cd src/frontend && npx tsc --noEmit -p tsconfig.app.json
```

Migrations run automatically at startup. To add one, from the relevant project:
`dotnet ef migrations add <Name>` — see the `ef-migration` skill. Each service has its **own** DbContext and migration history; never mix them.

Secrets are in .NET user-secrets, never the repo: Auth019 holds `AdminSeed:Email`, `AdminSeed:Password`, and optionally `Google:ClientId`/`Google:ClientSecret`.

**Verify changes against running services, not just a green build.** Most of this project's bugs were found that way — see the list in STATUS.md.

---

## Traps that have already bitten

**Adding a non-nullable column with a C# default.** EF backfills existing rows with the *SQL type default*, not your initializer. `IsActive = true` generated `defaultValue: false` and deactivated every account. Always add a matching `HasDefaultValue(...)` and **read generated migrations before applying them.**

**OpenIddict requires HTTPS.** `DisableTransportSecurityRequirement()` is Development-only. Never relax it elsewhere.

**Token exchange takes the subject token from the request body**, so `TokenExchangeHandler` authenticates with the *server* scheme, not the validation scheme.

**Everyone must agree on one issuer string.** `AppHost` pins `authIssuer` and passes it to both services; Auth019 would otherwise infer it from the proxy host and the API would reject every token.

**Vite must honour Aspire's `PORT`** (`vite.config.ts`, `strictPort: true`), or it drifts to another port and the proxy points at nothing.

**Query filters + eager loading**: an `Include` of a filtered entity silently drops archived rows.

**Windows testing**: use PowerShell `Invoke-RestMethod`, not `curl` (it mangles JSON bodies here). `sqlcmd` needs `-I` to `UPDATE` tables with filtered indexes — without it the statement fails while looking like it worked.

**Long-running dev servers**: stop them when done; don't leave orphaned `dotnet`/`node`/`dcp` processes holding ports.

---

## Before you finish

- Run `dotnet test` and the frontend typecheck.
- Exercise what you changed against running services.
- **Update [docs/STATUS.md](docs/STATUS.md)** — its whole purpose is telling the next person where things stand. Close a gap, say so; find a new one, record it.
- Update [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) if you changed a rule, [docs/API.md](docs/API.md) if you changed an endpoint.
- Report honestly: if something is untested or partly done, say which part.
