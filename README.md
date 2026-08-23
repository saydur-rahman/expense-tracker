# expensetracker019

A free, mobile-first expense tracker. People group their spending into **Categories**, each holding **Heads** (the actual line items they spend against), set a budget per month at both levels, and log expenses against heads.

Built as **two independent services** behind an OAuth 2.0 authorization server, orchestrated locally with **.NET Aspire**:

| Service | Role |
|---|---|
| **Auth019** | OAuth 2.0 / OpenID Connect server (OpenIddict). Owns all user accounts, sign-in, roles, and user administration. |
| **ExpenseTracker019.Api** | Pure resource server. Owns the expense domain. Holds no user table — it only validates tokens Auth019 issued. |
| **frontend** | React SPA. Signs in via redirect to Auth019 (Authorization Code + PKCE); never sees a password. |

Each service has its **own database**, so neither can reach into the other's schema.

---

## Quick start

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node.js](https://nodejs.org) 20+, and **Docker Desktop running** (Aspire runs SQL Server in a container).

```bash
cd src/ExpenseTracker019.AppHost
dotnet run
```

That single command starts SQL Server, both services, and the frontend, and prints a link to the **Aspire dashboard** where you can see every service, its logs, and its traces.

The frontend is at **http://localhost:5173**. Both databases are created and migrated automatically on first run.

### Signing in

Set a seed admin once (from `src/Auth019`):

```bash
dotnet user-secrets set "AdminSeed:Email" "you@example.com"
dotnet user-secrets set "AdminSeed:Password" "SomethingStrong!"
```

On startup Auth019 creates that account (if missing) and grants it the **Admin** role. Anyone else can self-register from the sign-in page.

> **Note on ports:** Aspire allocates service ports dynamically. Use the dashboard to find Auth019 and the API — only the frontend is pinned to 5173.

---

## What it does

| Feature | Notes |
|---|---|
| **Categories & Heads** | Two levels. Rename anytime. "Removing" archives — your past data is never deleted. |
| **Custom month cycle** | Your month can start on any day. Set it to 25 to track salary-to-salary instead of calendar months. |
| **Monthly budgets** | Budget per category, then split it across that category's heads. Heads can never total more than their category. |
| **Per-month reset** | Clear one month's budgets without touching other months or your category setup. |
| **Expenses** | Logged against a head, with date and optional note. Filter history by head or date range. |
| **Dashboard** | Budget vs actual per category and head, with over-budget flags. |
| **Sign-in** | Email/password or Google, handled entirely by Auth019 over standard OAuth 2.0. |
| **Admin** | List/search users, see last login, deactivate/reactivate, and view a user's account **read-only** for support. |

---

## Documentation

| Document | What's in it |
|---|---|
| **[docs/STATUS.md](docs/STATUS.md)** | What's built, what isn't, known gaps, and what to do next. **Start here when picking the project back up.** |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | How the two services fit together, the domain rules, and why key decisions were made. |
| [docs/API.md](docs/API.md) | Endpoint and OAuth flow reference. |
| [docs/PLAN.md](docs/PLAN.md) | The original implementation plan (historical). |
| [CLAUDE.md](CLAUDE.md) / [AGENTS.md](AGENTS.md) | Orientation for AI coding assistants. |

---

## Project layout

```
expensetracker019.sln
Directory.Build.props            shared TFM / language settings
Directory.Packages.props         central package versions
src/
  Auth019/                       OAuth2 + OIDC server, Identity, user admin
    Controllers/                 authorization + admin endpoints
    Pages/Account/               sign-in, register, external login (server-rendered)
    Services/                    token exchange, claim destinations, admin service
  ExpenseTracker019.Api/         resource server: the expense domain
    Models/ Data/ Services/ Controllers/ Authorization/
  ExpenseTracker019.AppHost/     Aspire orchestration
  ExpenseTracker019.ServiceDefaults/  telemetry, health checks, resilience
  frontend/                      React SPA
tests/ExpenseTracker019.Tests/   xUnit
.claude/skills/                  task recipes (run-dev, ef-migration, …)
```

---

## Tech stack

**Backend** — .NET 10, ASP.NET Core, EF Core, SQL Server, OpenIddict 7, ASP.NET Identity.
**Frontend** — React 19 + TypeScript (Vite), oidc-client-ts, React Router, TanStack Query, Tailwind CSS v4.
**Orchestration** — .NET Aspire 13.

---

## Testing

```bash
dotnet test                                    # unit tests
cd src/frontend && npx tsc --noEmit -p tsconfig.app.json   # typecheck
```

For manual API and OAuth checks, see `.claude/skills/api-smoke-test/SKILL.md`.
