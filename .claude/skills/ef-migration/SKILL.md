---
name: ef-migration
description: Create or apply an Entity Framework Core migration for either expensetracker019 database (Auth019 identity, or the expense domain). Use whenever entity changes need a migration.
---

# ef-migration

**There are two independent databases**, each with its own DbContext and migration history. Never mix them.

| Database | Project | DbContext | Holds |
|---|---|---|---|
| `auth019db` | `src/Auth019` | `AuthDbContext` | Identity + OpenIddict tables |
| `expensedb` | `src/ExpenseTracker019.Api` | `AppDbContext` | Categories, heads, budgets, expenses |

## Create a migration

From the relevant project directory:
```
dotnet ef migrations add <DescriptiveName>
```
Use short PascalCase names (e.g. `AddExpenseNoteMaxLength`).

Both projects have an `IDesignTimeDbContextFactory`, so the tooling works without Aspire running — it falls back to a local SQL Server. Override with the `Auth019Db` / `ExpenseDb` environment variables if your local server differs.

## Apply migrations

**Normally you don't need to** — both services call `Database.MigrateAsync()` at startup, so running the AppHost applies anything pending.

To apply manually: `dotnet ef database update` from the relevant project.

## Remove the last (unapplied) migration

```
dotnet ef migrations remove
```
Only safe if it hasn't been applied anywhere shared.

## ⚠️ Always read the generated migration before applying

Adding a **non-nullable column with a C# default** is the trap that has already caused a real bug here: EF backfills existing rows with the *SQL type default*, not your C# initializer. `IsActive = true` produced `defaultValue: false` and deactivated every account.

Fix by configuring it in the DbContext too:
```csharp
e.Property(u => u.IsActive).HasDefaultValue(true);
```

Also check query filters after any model change — `Category` and `Head` carry soft-delete filters that history and report queries must bypass with `IgnoreQueryFilters()`.
