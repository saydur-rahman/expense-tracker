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
| [docs/DEPLOY.md](docs/DEPLOY.md) | Deploying to Azure on free tiers, and what that costs you in constraints. |
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

   **Auth019's tables live in the `auth` schema**, the expense API's in `dbo`, each with its own migration-history table. That is what lets the two share a single database in hosting that only offers one for free — they still share no tables. Don't remove the schema or the `MigrationsHistoryTable` call: the two services would then overwrite each other's migration history. See [docs/DEPLOY.md](docs/DEPLOY.md).

2. **Heads are authoritative: a category's budget is what its heads add up to.** Once any head in a category carries a budget, the category's budget *is* their total — in `BudgetService` for the editor and in `ReportService` for the dashboard. Both must agree; if you change the rule, change it in both.

   The figure stored on the category is only a **target**. It never caps the heads, it is never required before them, and it stands in as the budget solely when **no** head has been budgeted at all. Heads over or under the target are reported back as "X extra" / "X short" — never refused. Clearing the target leaves the head budgets alone.

   *(This replaces the original rule, where the category budget was a hard ceiling that had to exist first and whose clearing wiped its heads. Nothing enforces a ceiling any more — don't reintroduce one without being asked.)*

   Carry-forward copies head budgets **independently** of category targets, or a user who only ever fills in heads loses their whole budget at the turn of the period.

   **The dashboard and the Budgets screen draw the same overview strip, and both feed it from `/api/reports/summary`** — one query, one key (`['summary', periodId]`). `PeriodBudgetsDto` deliberately carries no period totals: it once did, computed over a slightly different set of categories than `ReportService` uses, which is exactly how the same strip would print two different budgets on two screens. Any write that moves a budget, a category or an expense must invalidate `['summary']` as well as its own key.

3. **Categories and Heads are never hard-deleted.** `IsArchived` + EF global query filters. Their past expenses and budgets must stay visible in history and reports — which is why those queries call `IgnoreQueryFilters()`. **If you add a history or report query, it needs that too**, or archived data silently vanishes from the views meant to preserve it.

4. **A period's boundaries are always computed from the user's current cycle setting.** A user budgets **monthly** (from a day of the month) or **weekly** (from a day of the week) — one rhythm at a time, `PeriodKind` on the setting row. `MonthCycleMath` does the arithmetic for both (unit-tested — extend those tests if you touch it); `BudgetPeriod` rows exist only as a stable anchor for budgets to hang off and **never override the calculation**. Changing the setting re-cuts the current period immediately. A row cut under an older setting that shares a start date is realigned to the new end date. *(This reverses the earlier "an existing period covering a date always wins" rule, which meant a cycle change silently did nothing until the next month.)*

   **`Kind` is part of a period's identity, not decoration.** The unique index is `(UserId, Kind, StartDate)`, and every period lookup filters on it — a week and a month can legitimately start on the same day, and without the filter the realign above rewrites one into the other and strands its budgets. Carry-forward filters on it too: a month's figure landing in a week is an amount the user never chose, so a freshly switched rhythm starts empty.

   **Enum columns with `HasDefaultValue` need `ValueGeneratedNever()`.** EF treats a store-default property as store-generated and sends `DEFAULT` for any value equal to the CLR default — so a user picking `DayOfWeek.Sunday` (0) was silently given the column's default of Monday. This only shows up against a real database, never in a build.

   **A new month inherits the previous month's budgets.** Seeding runs backwards-only, never over a period that already has budgets, and **at most once per period** — `BudgetsInitialized` is set both when a period is seeded and by every budget write, so a month the user emptied on purpose stays empty. If you add a budget write path, set that flag.

5. **Impersonation is read-only, enforced as a scope check.** The impersonation token carries `expense.read` only, no roles, no refresh token. `RequireWriteScopeFilter` is registered **globally** so any new write endpoint is protected by default — **don't add per-endpoint exceptions**, and don't make the expense API aware of impersonation as a concept.

6. **Deactivation must be checked at every token-issuing path** (authorize, refresh, token exchange).

   **Logout must revoke tokens, not just clear the cookie**, or refresh tokens outlive the session. And the SPA's `post_logout_redirect_uri` must stay a **public** route (`/signed-out`) — pointing it at a protected one restarts sign-in immediately, which an external provider answers silently and makes logout look broken. Fix that landing page, not the provider: the external challenge deliberately sends **no** `prompt`, so a user already signed in to Google goes straight through.

   **So must profile completeness.** `Authorize()` refuses to mint a token for an account with no `Country` and sends it to `/Account/CompleteProfile` first — that is what stops an external (Google) sign-up, which never sees the registration form, from ending up with no currency. Keep the check on the token-issuing path, not the sign-in callback, or a stale session cookie walks straight past it.

   **Impersonation is read-only in Auth019 too.** `PUT /api/profile` refuses a token carrying `imp_by`. Any new Auth019 write endpoint reachable by an ordinary user needs the same check — the `auth.admin` scope doesn't cover it, because profile writes don't require that scope.

7. **Every expense-API query is scoped by the `sub` claim** via `ICurrentUser`. Never trust a client-supplied user id. This — not the archive filter — is the tenant isolation boundary.

8. **Expense and income are separate ledgers that must never cross.** `Category.Kind` decides which one a category (and every head under it) belongs to. Income takes **no budget**, ever. Three service-level guards enforce it — `BudgetService` rejects budgets on income, `ExpenseService` rejects spending against an income head, `IncomeService` rejects income against a spending head. Any new query over categories, heads, budgets or totals must filter by `Kind`, or the dashboard's figures silently mix the two.

---

## Conventions

- **Business logic goes in `Services/`**, not controllers. Controllers bind, delegate, return.
- **Errors**: throw `AppException` subclasses (400/403/404/409); middleware maps them. Write messages for end users saying what to do next — match the tone of the budget errors.
- **Frontend server state** goes through TanStack Query; invalidate query keys after mutations.
- **`auth/jwt.ts` decodes tokens for display only** — never a security decision. The API validates every token.
- **Colour is semantic**: `brand-*` (blue) for actions and selection, `positive-*` (green) for money still held, `negative-*` (red) for trouble and destructive actions. Those three carry everything. Two more exist for exactly one job — the rungs above "your income covers your budget" on the dashboard's overview strip, where green is already taken: `surplus-*` (yellow, income 20% clear) and `gold-*` (income 50% clear). They are used as **bar fills and as tinted pills, never as ink** — yellow cannot reach 4.5:1 as text on a light card — and each rung's pill also carries a glyph so the two are told apart without relying on hue. Don't reach for either outside that ladder, and don't delete them as stray warning colours: `lib/budgetHealth.ts` owns the thresholds. **Surfaces use the semantic roles** `page`/`card`/`raised`/`input`/`track`/`line`/`line-soft`/`ink`/`ink-soft`/`ink-muted`, which resolve per theme on their own — write `bg-card`, never `bg-white dark:bg-gray-900`, and never reach for raw `gray-*`/`indigo-*`. Reuse `components/Button.tsx` and the class constants in `components/ui.ts` rather than re-typing card/input classes. **If you change a chart colour, re-run the data-viz validator against both card surfaces.**
- **Mobile-first CSS**: base styles narrow, `md:`/`lg:` on top.
- **Money** is `decimal(18,2)`, configured explicitly.
- **Package versions** are centralized in `Directory.Packages.props`; don't put `Version=` in a csproj.
- **Say when something is getting messy — the owner has asked to be told.** If a change is turning complicated, duplicating something that already exists, painting the design into a corner, or the logic has genuinely defeated you, raise it *in the same message as the work*: name the file or pattern, say why it is a problem, propose a fix, and let them decide. Deliver what was asked as well — this is not licence to hand ordinary fiddly work back. Their words: *"if there is any code that gets complicated and redundant and scalable and you cant do the logic let me know i will help always."*
- **Listing history must not write.** `ResolveRelativePeriodAsync` (and so `current` / `relative/{offset}`) *creates* the `BudgetPeriod` row and runs carry-forward — stepping to a cycle is a write. Anything that merely enumerates cycles must compute the windows instead, as `ListRecentWindowsAsync` does; resolving a list of them would create rows wholesale and could carry budgets into periods the user never budgeted.
- **Amount inputs go through `lib/calc`.** Every field that takes money accepts arithmetic (`635*3`), so use `components/AmountField` — or `readAmount`/`amountValue` for a field with its own layout — rather than `Number(input)`. The evaluator is a hand-written parser, deliberately not `eval`: keep it that way, and keep the grammar to `+ - * / ( )` over numbers.
- **The help page is part of the feature.** `src/frontend/src/pages/HelpPage.tsx` is the app's user-facing documentation. Any change that alters what a user sees or does — a new screen, a renamed control, a rule that behaves differently, an option that appears or disappears — **updates that page in the same change**, in the user's words rather than the code's. Use the `help-page` skill. A change that touches no user-visible behaviour (a refactor, a migration, a test) needs nothing.
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
