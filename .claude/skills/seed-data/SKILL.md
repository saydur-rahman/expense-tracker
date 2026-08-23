---
name: seed-data
description: Seed the local expensetracker019 databases with a demo user and sample categories, heads, budgets, and expenses. Use when sample data is needed to exercise the dashboard without manual entry.
---

# seed-data

## What already seeds automatically

Auth019 seeds on every startup: the `User` and `Admin` roles, the OAuth scopes, the SPA client registration, and the admin account from `AdminSeed:Email` / `AdminSeed:Password`. You do **not** need to do anything for those.

## What doesn't exist yet

There is no seeding of **expense domain** data (categories, heads, budgets, expenses). When asked to seed:

1. First search `src/ExpenseTracker019.Api` for "seed" — a mechanism may have been added since this was written.
2. If none exists, build one: an idempotent routine that creates a couple of Categories with Heads, the current `BudgetPeriod` with Category/Head budgets **that satisfy the sum constraint**, and a handful of Expenses across the period.
3. Prefer a console-argument entry point (`dotnet run -- seed`) over an HTTP endpoint, so it can't be reached in a deployed environment.

Note the cross-service wrinkle: expense data is keyed by a user id that lives in **Auth019's** database. A seeder needs a real user id — either read it from `auth019db`, or accept it as an argument.

Update this file with the actual command once it exists.

## Quick manual alternative

Run the stack, sign in as the seed admin, and use the API directly — `.claude/skills/api-smoke-test/SKILL.md` walks through creating categories, heads, budgets, and expenses in order.
