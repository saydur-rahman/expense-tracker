---
name: db-reset
description: Drop and recreate the local Expense Tracker development database from scratch, then reapply all migrations. Use when the local database is in a broken/inconsistent state (e.g. after editing an already-applied migration, or during early schema churn) and needs a clean slate.
---

# db-reset

Recovers from a broken local migration history by dropping the dev database and rebuilding it from the current migrations.

## Steps

From `backend/ExpenseTracker.Api`:
```
dotnet ef database drop --force
dotnet ef database update
```

Optionally re-seed afterward using the `seed-data` skill so there's demo data to work with again.

## Warning

This permanently deletes all data in the local `ExpenseTrackerDb` database. Only use it for the local development database — never against a shared or production database. Confirm with the user before running this if there's any chance the local database holds data they care about (e.g. manually entered test expenses they want to keep).
