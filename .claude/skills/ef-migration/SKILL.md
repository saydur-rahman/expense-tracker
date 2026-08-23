---
name: ef-migration
description: Create or apply an Entity Framework Core migration for the Expense Tracker backend (SQL Server). Use whenever entity/model changes need a new migration, or the local database needs to be brought up to date.
---

# ef-migration

Wraps the `dotnet ef` commands with the right working directory so migrations are always created/applied consistently, without needing to remember `--project`/`--startup-project` flags (this project uses a single project, so none are needed as long as commands run from `backend/ExpenseTracker.Api`).

## Create a new migration

From `backend/ExpenseTracker.Api`:
```
dotnet ef migrations add <DescriptiveName>
```
Use a short PascalCase name describing the change (e.g. `AddExpenseNoteMaxLength`).

## Apply migrations to the local database

From `backend/ExpenseTracker.Api`:
```
dotnet ef database update
```
Applies any pending migrations to the database named in `appsettings.Development.json`'s `ConnectionStrings:Default`.

## Remove the last (unapplied) migration

```
dotnet ef migrations remove
```
Only safe if that migration hasn't been applied to a shared database yet.

## Notes

- All entities live in `backend/ExpenseTracker.Api/Models/`; `AppDbContext` (in `Data/AppDbContext.cs`) is where relationships, indexes, and query filters (soft-delete on Category/Head) are configured — check both after any model change, since EF Core migrations are generated from `OnModelCreating`.
- `dotnet ef` requires the `dotnet-ef` global tool (`dotnet tool install --global dotnet-ef` if missing).
