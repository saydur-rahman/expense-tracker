# Agent Guide — Expense Tracker

Orientation for AI coding assistants. Read this before making changes.
(`CLAUDE.md` points here — this is the single source of truth for both.)

---

## Orient yourself first

| Read | For |
|---|---|
| **[docs/STATUS.md](docs/STATUS.md)** | **Start here.** What's built, what isn't, known gaps, next steps. |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | The domain rules and *why* they're built that way. |
| [docs/API.md](docs/API.md) | Endpoint reference. |
| [README.md](README.md) | Setup and running. |
| [docs/PLAN.md](docs/PLAN.md) | Original plan (historical; may not match current state). |

**The docs are current as of 2026-08-23.** Verify against the code before relying on a specific detail — if they disagree, the code wins, and please fix the doc.

---

## What this is

A free, mobile-first expense tracker. Users group spending into **Categories** → **Heads**, budget per month at both levels, and log expenses against heads. Web app now, intended to become a mobile app later — which is why the API and SPA are cleanly separated and auth uses bearer tokens rather than cookies.

**Stack:** ASP.NET Core 8 + EF Core + SQL Server; React 19 + TypeScript (Vite) + Tailwind v4 + TanStack Query.

```
backend/ExpenseTracker.Api/   Models/ Data/ Services/ Controllers/ Dtos/ Middleware/
backend/tests/                xUnit
frontend/src/                 api/ auth/ components/ features/ layouts/ pages/
.claude/skills/               run-dev, ef-migration, db-reset, seed-data, api-smoke-test
```

---

## Rules you must not break

These encode explicit product requirements. **Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before touching any of them.**

1. **A category's head budgets can never exceed the category's own budget for that period.** Lives in `BudgetService`. Not a DB constraint — it's a cross-row SUM. A category budget must exist before any head budget under it; clearing a category's budget for a month clears its heads' budgets for that month too.

2. **Categories and Heads are never hard-deleted.** `IsArchived` + EF global query filters. Their past expenses and budgets must stay visible in history and reports — which is why those queries use `IgnoreQueryFilters()`. **If you add a history or report query, it needs that too**, or archived data silently disappears from the very views meant to preserve it.

3. **A "month" is a stored `BudgetPeriod` row**, not calendar math. Users pick their own cycle start day. An existing period covering a date always wins, so changing a cycle never re-cuts periods that budgets are attached to. Date edge cases live in `MonthCycleMath` (unit-tested — extend those tests if you touch it).

4. **Impersonation is read-only.** `ImpersonationReadOnlyMiddleware` blocks every non-GET centrally, so new write endpoints are safe by default. **Don't add per-endpoint exceptions.** Impersonation tokens carry no roles, can't reach `/api/admin/*`, and can't target an admin.

5. **Every query is scoped by the authenticated user's id** from the JWT `sub` claim via `ICurrentUser`. Never trust a client-supplied user id. This — not the archive filter — is the tenant isolation boundary.

---

## Conventions

- **Business logic goes in `Services/`**, not controllers. Controllers stay thin: bind, delegate, return.
- **Errors**: throw `AppException` subclasses (`ValidationAppException` → 400, `ForbiddenAppException` → 403, `NotFoundAppException` → 404, `ConflictAppException` → 409). Middleware maps them. Write messages for end users, saying what to do next — match the tone of the existing budget errors.
- **Frontend server state** goes through TanStack Query; invalidate query keys after mutations rather than hand-patching caches.
- **Mobile-first CSS**: base styles for narrow screens, `md:`/`lg:` on top.
- **Money** is `decimal(18,2)`, configured explicitly in `AppDbContext`.
- Match surrounding style. Comments explain *why*, not *what*.

---

## Working on this

```bash
# Backend  (Swagger at /swagger)
cd backend/ExpenseTracker.Api && dotnet run --urls "http://localhost:5080"
# Frontend
cd frontend && npm run dev

# Checks
cd backend && dotnet test
cd frontend && npx tsc --noEmit -p tsconfig.app.json
```

Migrations: `dotnet ef migrations add <Name>` then `dotnet ef database update` from `backend/ExpenseTracker.Api`. See the `ef-migration` and `db-reset` skills.

Secrets are in .NET user-secrets, never the repo: `Jwt:Key`, `Google:ClientId`, `AdminSeed:Email`.

**Verify changes against a running API, not just a green build.** Most of this project's bugs were found that way. `.claude/skills/api-smoke-test/SKILL.md` has a checklist.

---

## Traps that have already bitten

**Adding a non-nullable column with a C# default.** EF backfills existing rows with the *SQL type default*, not your initializer. `IsActive = true` generated `defaultValue: false` and deactivated every existing account. Always add a matching `HasDefaultValue(...)` in `AppDbContext`, and **read generated migrations before applying them.**

**JWT claim remapping.** `MapInboundClaims = false` in `Program.cs` keeps `sub` as `sub`. Don't remove it — `ICurrentUser` depends on it.

**Query filters + eager loading.** An `Include` of a filtered entity silently drops archived rows.

**Windows API testing.** Use PowerShell `Invoke-RestMethod`, not `curl` — `curl` mangles JSON bodies and hides error payloads here.

**Long-running dev servers.** Start them detached and stop them when done; don't leave orphaned `dotnet`/`node` processes holding ports 5080/5173.

---

## Before you finish

- Run `dotnet test` and the frontend typecheck.
- Exercise what you changed against a running app.
- **Update [docs/STATUS.md](docs/STATUS.md)** — its whole purpose is telling the next person where things stand. If you close a gap, say so; if you find a new one, record it.
- Update [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) if you changed a rule, and [docs/API.md](docs/API.md) if you changed an endpoint.
- Report honestly: if something is untested or partly done, say which part.
