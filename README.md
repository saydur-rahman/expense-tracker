# Expense Tracker

A free, mobile-first expense tracker. People group their spending into **Categories**, each holding **Heads** (the actual line items they spend against), set a budget per month at both levels, and log expenses against heads.

Built web-first but deliberately mobile-friendly, so it can be wrapped into a native mobile app later without a rewrite.

---

## Quick start

**Prerequisites:** .NET 8 SDK, Node.js 20+, SQL Server (LocalDB, Express, or full), and the EF Core CLI (`dotnet tool install --global dotnet-ef`).

```bash
# 1. Set up secrets (one time)
cd backend/ExpenseTracker.Api
dotnet user-secrets set "Jwt:Key" "<a base64 64-byte key>"
dotnet user-secrets set "AdminSeed:Email" "you@example.com"

# 2. Create the database
dotnet ef database update

# 3. Run the API  (http://localhost:5080, Swagger at /swagger)
dotnet run --urls "http://localhost:5080"

# 4. In another terminal, run the frontend  (http://localhost:5173)
cd frontend
npm install
npm run dev
```

Then register an account at http://localhost:5173. To become an admin, register with the email you set as `AdminSeed:Email`, then restart the API — it grants the Admin role on startup.

Generate a JWT key with:
```powershell
$rng=[Security.Cryptography.RandomNumberGenerator]::Create();$b=New-Object byte[] 64;$rng.GetBytes($b);[Convert]::ToBase64String($b)
```

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
| **Accounts** | Email/password or Google sign-in. |
| **Admin** | List/search users, see last login, deactivate/reactivate, and view a user's account read-only for support. |

---

## Documentation

| Document | What's in it |
|---|---|
| **[docs/STATUS.md](docs/STATUS.md)** | What's built, what isn't, known gaps, and what to do next. **Start here when picking the project back up.** |
| **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** | The domain model, the business rules that aren't obvious from the code, and why key decisions were made. |
| **[docs/PLAN.md](docs/PLAN.md)** | The original implementation plan, kept as a record of intended scope. |
| **[docs/API.md](docs/API.md)** | Endpoint reference. |
| **[CLAUDE.md](CLAUDE.md)** / **[AGENTS.md](AGENTS.md)** | Orientation for AI coding assistants. |

---

## Project layout

```
backend/
  ExpenseTracker.Api/       ASP.NET Core 8 Web API
    Models/                 EF Core entities
    Data/                   DbContext, migrations, role seeding
    Services/               Business logic (BudgetService holds the core rule)
    Controllers/            REST endpoints
    Middleware/             Error handling, impersonation read-only guard
  tests/ExpenseTracker.Tests/   xUnit tests
frontend/
  src/
    api/                    Typed API client per resource
    auth/                   Auth context and route guards
    features/               One folder per screen area
    layouts/                App shell (bottom nav on mobile, top nav on desktop)
.claude/skills/             Task recipes (run-dev, ef-migration, db-reset, …)
```

---

## Tech stack

**Backend** — ASP.NET Core 8, EF Core, SQL Server, ASP.NET Identity with JWT bearer tokens.
**Frontend** — React 19 + TypeScript (Vite), React Router, TanStack Query, Tailwind CSS v4, React Hook Form + Zod.

The API is a pure REST backend and the frontend a standalone SPA — that separation is what makes the eventual mobile port straightforward.

---

## Testing

```bash
cd backend && dotnet test          # unit tests
cd frontend && npx tsc --noEmit -p tsconfig.app.json   # typecheck
```

For manual API checks, see `.claude/skills/api-smoke-test/SKILL.md`.
