---
name: seed-data
description: Seed the local Expense Tracker database with a demo user and sample categories, heads, budget periods, budgets, and expenses. Use when the user wants sample data to exercise the dashboard/reports without manual data entry.
---

# seed-data

Populates the local dev database with realistic sample data so screens like the dashboard and budget setup have something to show immediately.

## Status

Not yet implemented as a runnable command — there is no seeding script/endpoint yet. When asked to seed data:

1. Check whether `backend/ExpenseTracker.Api` has gained a seed mechanism since this skill was written (e.g. a `Data/SeedData.cs`, a `dotnet run -- seed` argument, or a dedicated `/api/dev/seed` endpoint guarded to Development only) — search for "seed" in the backend project first.
2. If none exists, build one: a small idempotent routine that, on a Development-only trigger, creates (or reuses) a demo user (e.g. `demo@example.local`), a couple of Categories with Heads under each, the current BudgetPeriod with Category/Head budgets that satisfy the sum-constraint, and a handful of Expenses spread across the period.
3. Prefer a `dotnet run --project backend/ExpenseTracker.Api -- seed` console-argument style entry point over a permanent HTTP endpoint, so it can't accidentally be hit in a deployed environment.

Update this skill file with the actual command once a seeding mechanism exists.
