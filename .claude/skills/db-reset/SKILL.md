---
name: db-reset
description: Drop and recreate the local expensetracker019 development databases from scratch. Use when local data is in a broken or inconsistent state and needs a clean slate.
---

# db-reset

Two databases exist — `auth019db` (identity) and `expensedb` (expense domain). Decide whether you need to reset one or both.

**Resetting only `auth019db` orphans the expense data**, because expense rows carry the old user ids. For a genuinely clean slate, reset both.

## With Aspire (the usual case)

Aspire's SQL Server container keeps its data in a volume (`.WithDataVolume()`), so data survives restarts. To wipe it:

1. Stop the AppHost.
2. Remove the container and its volume:
   ```
   docker ps -a --filter "name=sql" --format "{{.Names}}"
   docker rm -f <container-name>
   docker volume ls --filter "name=sql" --format "{{.Name}}"
   docker volume rm <volume-name>
   ```
3. Start the AppHost again — both databases are recreated, migrated, and reseeded.

## Standalone local SQL Server

From the relevant project:
```
dotnet ef database drop --force
dotnet ef database update
```

## ⚠️ Warning

This permanently deletes all local data, including any accounts you registered. Only ever do it against local development databases. **Confirm with the user first** if there's any chance the local data matters to them.

After a reset, Auth019 recreates the seed admin from `AdminSeed:Email` / `AdminSeed:Password` in user-secrets; everyone else must re-register.
