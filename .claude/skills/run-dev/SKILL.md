---
name: run-dev
description: Start the whole expensetracker019 stack (SQL Server, Auth019, the expense API, and the React frontend) with .NET Aspire. Use when the user wants to run, start, or preview the app locally.
---

# run-dev

One command starts everything — Aspire orchestrates SQL Server, both services, and the frontend.

## Prerequisite

**Docker Desktop must be running** — Aspire runs SQL Server in a container. Check with `docker ps`; if it fails, start Docker Desktop and wait for the daemon (`until docker ps >/dev/null 2>&1; do sleep 5; done`).

## Run

From `src/ExpenseTracker019.AppHost`:
```
dotnet run
```

The console prints a link to the **Aspire dashboard**, which lists every resource with its URL, logs, and traces. The frontend is at **http://localhost:5173**.

Both databases are created and migrated automatically at startup, and Auth019 seeds roles, OAuth scopes, the SPA client registration, and the configured admin.

## Notes

- **Only the frontend has a fixed port.** Auth019 and the expense API get dynamically allocated ports — read them from the dashboard rather than assuming.
- First run is slow: it pulls the SQL Server image. If the Aspire CLI reports a start timeout, raise it: `ASPIRE_CLI_START_TIMEOUT=600 dotnet run`.
- Running over plain HTTP locally needs `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` in some setups.
- To stop cleanly, Ctrl+C the AppHost. If run detached, kill the `dcp`, `Auth019`, `ExpenseTracker019.Api`, and `node` processes — orphans hold ports and block the next run.

## Running a service on its own

Occasionally useful for debugging one service. Supply the connection string and issuer yourself:

```
# Auth019
cd src/Auth019
ConnectionStrings__auth019db="Server=.;Database=Auth019Db;Trusted_Connection=True;TrustServerCertificate=True;" \
  dotnet run --urls "http://localhost:5090"

# Expense API (needs Auth019 running first)
cd src/ExpenseTracker019.Api
ConnectionStrings__expensedb="Server=.;Database=ExpenseTracker019Db;Trusted_Connection=True;TrustServerCertificate=True;" \
  Auth019__Issuer="http://localhost:5090/" dotnet run --urls "http://localhost:5080"
```

The issuer must match exactly what Auth019 puts in `iss`, or the API rejects every token with `invalid_token`.
