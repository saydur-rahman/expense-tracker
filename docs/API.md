# API Reference

Base URL in development: `http://localhost:5080`. Interactive docs at `/swagger`.

All endpoints except the auth ones require `Authorization: Bearer <accessToken>`, and every resource is scoped to the authenticated user — ids from another user's account return 404, never their data.

Errors come back as RFC 7807 problem details: `{ "title": "...", "status": 400 }`.

---

## Auth

| Method | Path | Notes |
|---|---|---|
| POST | `/api/auth/register` | `{email, password, displayName}` → tokens + user. Assigns the `User` role. |
| POST | `/api/auth/login` | `{email, password}` → tokens + user. 403 if deactivated. |
| POST | `/api/auth/google` | `{idToken}` from Google Identity Services → tokens + user. |
| POST | `/api/auth/refresh` | `{refreshToken}` → new token pair. Old token is revoked (rotation). |
| GET | `/api/auth/me` | Current user, roles, and impersonation state. |

**Auth response:**
```json
{
  "accessToken": "eyJ…",
  "refreshToken": "base64…",
  "accessTokenExpiresAtUtc": "2026-08-23T04:41:57Z",
  "user": { "id": "…", "email": "…", "displayName": "…", "roles": ["User"],
            "isImpersonating": false, "impersonatedBy": null }
}
```

Access tokens last 15 minutes, refresh tokens 30 days (configurable under `Jwt`).

---

## Settings

| Method | Path | Notes |
|---|---|---|
| GET | `/api/settings/month-cycle` | `{startDay, isConfigured}`. Defaults to day 1 when unset. |
| PUT | `/api/settings/month-cycle` | `{startDay}` (1–31). Appends a new effective-dated setting. |

---

## Budget periods

A period is one user's "month", resolved from their cycle and created on demand.

| Method | Path | Notes |
|---|---|---|
| GET | `/api/budget-periods/current` | The period containing today. |
| GET | `/api/budget-periods/relative/{offset}` | `-1` previous, `1` next, etc. |
| GET | `/api/budget-periods/{id}` | One period. |
| GET | `/api/budget-periods` | All periods, newest first. |

Returns `{id, startDate, endDate, label}` — e.g. `"25 Jul – 24 Aug 2026"`.

---

## Categories & Heads

| Method | Path | Notes |
|---|---|---|
| GET | `/api/categories?includeArchived=false` | Categories with nested heads. |
| POST | `/api/categories` | `{name}`. 409 if the name is taken by a live category. |
| PUT | `/api/categories/{id}` | `{name}` — rename. |
| DELETE | `/api/categories/{id}` | **Archives** (cascades to heads). Data is kept. |
| POST | `/api/categories/{categoryId}/heads` | `{name}`. |
| PUT | `/api/heads/{id}` | `{name}` — rename. |
| DELETE | `/api/heads/{id}` | **Archives**. Data is kept. |

---

## Budgets

| Method | Path | Notes |
|---|---|---|
| GET | `/api/budget-periods/{periodId}/budgets` | All categories + heads with amounts and remaining allowance. |
| PUT | `/api/budget-periods/{periodId}/categories/{categoryId}/budget` | `{amount}`. |
| DELETE | `/api/budget-periods/{periodId}/categories/{categoryId}/budget` | Clears it **and its heads' budgets** for this period only. |
| PUT | `/api/budget-periods/{periodId}/heads/{headId}/budget` | `{amount}`. Enforces the category ceiling. |
| DELETE | `/api/budget-periods/{periodId}/heads/{headId}/budget` | Clears this head's budget for this period. |

All four return the full updated period budget, so the client needs no follow-up fetch.

**Rejections you should expect (400):**
- Setting a head budget with no category budget → *"Set a budget for the category first…"*
- Heads exceeding the category total → names the remaining allowance
- Lowering a category below its heads' sum → names the current head total

Response shape:
```json
{
  "periodId": "…", "periodLabel": "Aug 2026",
  "startDate": "2026-08-01", "endDate": "2026-08-31",
  "categories": [{
    "categoryId": "…", "categoryName": "Food",
    "amount": 1000.00, "allocatedToHeads": 1000.00, "unallocated": 0.00,
    "heads": [{ "headId": "…", "headName": "Groceries", "amount": 700.00 }]
  }]
}
```

---

## Expenses

| Method | Path | Notes |
|---|---|---|
| GET | `/api/expenses` | Filters: `from`, `to`, `categoryId`, `headId`, `page`, `pageSize` (max 100). |
| POST | `/api/expenses` | `{headId, amount, expenseDate, note?}`. Amount must be > 0. |
| PUT | `/api/expenses/{id}` | Same body. |
| DELETE | `/api/expenses/{id}` | Hard delete. |

Listing returns `{items, totalCount, totalAmount, page, pageSize}`, newest first, and **includes expenses on archived heads** so history stays complete. Creating or editing against an archived head returns 404 — history is kept, but closed to new spending.

---

## Reports

| Method | Path | Notes |
|---|---|---|
| GET | `/api/reports/summary?periodId={id}` | Budget vs actual for that period. |
| GET | `/api/reports/summary/current` | Same for the current period. |

Per category and head: `budget`, `spent`, `remaining`, `isOverBudget`, `isArchived`, plus period totals. Archived items appear only when they hold that period's spending or budget.

---

## Admin

All require the `Admin` role (403 otherwise) and are **blocked during impersonation**.

| Method | Path | Notes |
|---|---|---|
| GET | `/api/admin/users` | `search` (email or name), `includeInactive`, `page`, `pageSize`. |
| GET | `/api/admin/users/{id}` | One user. |
| POST | `/api/admin/users/{id}/deactivate` | Blocks login **and** refresh; revokes existing refresh tokens. |
| POST | `/api/admin/users/{id}/reactivate` | Restores access. |
| POST | `/api/admin/users/{id}/impersonate` | Returns a 15-minute **read-only** token for that user. |

User rows include `roles`, `isActive`, `lastLoginAtUtc`, `deactivatedAtUtc`, `createdAtUtc`.

**Guardrails (400/403):** can't deactivate or impersonate yourself; can't impersonate an admin or a deactivated user.

### Using an impersonation token

Send it as the bearer token. It permits `GET` only — any write returns 403, as does any `/api/admin/*` call. It carries no roles and has no refresh token, so it simply expires after 15 minutes. Clients should keep the admin's own token aside to restore afterwards.
