---
name: api-smoke-test
description: Sanity-check the Expense Tracker backend API's key endpoints (auth, categories, budgets, expenses, reports, admin) without needing the frontend running. Use after backend changes to quickly verify nothing is broken end-to-end.
---

# api-smoke-test

Confirms the API is wired correctly after changes. Start the API first (`dotnet run --urls "http://localhost:5080"` from `backend/ExpenseTracker.Api`).

On Windows, prefer PowerShell's `Invoke-RestMethod` over `curl` — `curl` in this environment can mangle JSON bodies and swallow error payloads.

## Flow

1. **Health** — `GET /swagger/v1/swagger.json` returns 200 with the OpenAPI document.
2. **Register / login** — `POST /api/auth/register` (or `/login`) returns an access token, refresh token, and the user's roles.
3. **Authenticated read** — `GET /api/auth/me` with `Authorization: Bearer <token>` returns the user; without a token it must be 401.
4. **Month cycle** — `PUT /api/settings/month-cycle` with `{startDay}`, then `GET /api/budget-periods/current` and confirm the period boundaries match the cycle.
5. **Categories & heads** — create a category, add heads, rename, then `DELETE` one and confirm it disappears from `GET /api/categories` but is still present with `?includeArchived=true`.
6. **Budget constraint** (the app's core rule) — set a category budget, then set head budgets under it. Verify:
   - a head budget with no category budget set is rejected
   - heads summing exactly to the category total are allowed
   - exceeding by 0.01 is rejected with a message naming the remaining allowance
   - lowering the category budget below the current head sum is rejected
7. **Expenses & report** — `POST /api/expenses` against a head, then `GET /api/reports/summary/current` and confirm spent/remaining/over-budget figures are correct.
8. **Admin & security boundaries** (see also the plan's verification section):
   - a non-admin calling `/api/admin/users` gets 403
   - an admin can list and search users, and sees `lastLoginAtUtc`
   - deactivating a user blocks their next login **and** their refresh-token exchange
   - an impersonation token can GET the target's data but is 403 on any POST/PUT/DELETE
   - an impersonation token gets 403 on `/api/admin/*`, and admins cannot impersonate other admins

## Notes

- Run the subset relevant to whatever changed rather than the whole flow every time.
- Register a throwaway user per run when testing flows that mutate state, so runs stay independent.
