---
name: run-dev
description: Start the Expense Tracker backend (ASP.NET Core API) and frontend (Vite React dev server) together for local development. Use when the user wants to run, start, or preview the app locally.
---

# run-dev

Starts both halves of the stack so the app can be exercised end-to-end in a browser.

## Steps

1. Start the backend API (from `backend/ExpenseTracker.Api`):
   ```
   dotnet run --urls "http://localhost:5080"
   ```
   Swagger UI is available at `http://localhost:5080/swagger` in Development.

2. Start the frontend (from `frontend`):
   ```
   npm run dev -- --port 5173
   ```
   Served at `http://localhost:5173`. `frontend/.env.development` already points `VITE_API_BASE_URL` at `http://localhost:5080`, and the backend's CORS policy (`Cors:AllowedOrigins` in `appsettings.json`) already allows `http://localhost:5173`.

3. Run both in the background (e.g. `run_in_background` / separate terminals) so they can be watched together. Check `dotnet run` output for "Now listening on" and the Vite output for "Local:" to confirm both are up before opening a browser.

## Notes

- The backend needs a local SQL Server instance reachable via the connection string in `backend/ExpenseTracker.Api/appsettings.Development.json` (`Server=.;Database=ExpenseTrackerDb;...`) and the database migrated — see the `ef-migration` skill if `dotnet ef database update` hasn't been run yet.
- The JWT signing key and Google OAuth client ID live in .NET user-secrets for this project (`dotnet user-secrets list` from `backend/ExpenseTracker.Api` to inspect), not in any committed file.
