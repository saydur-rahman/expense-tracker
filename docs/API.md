# API Reference

Two services. **Auth019** issues tokens and owns users; **ExpenseTracker019.Api** owns the expense domain and only validates tokens.

Ports are allocated by Aspire at runtime — find them on the Aspire dashboard. Below, `{auth}` and `{api}` stand in for those base URLs.

Errors come back as RFC 7807 problem details (`{"title": "...", "status": 400}`); OAuth endpoints return standard OAuth errors (`{"error": "...", "error_description": "..."}`).

---

## Auth019 — OAuth 2.0 / OpenID Connect

### Discovery

| Endpoint | Purpose |
|---|---|
| `GET {auth}/.well-known/openid-configuration` | Discovery document |
| `GET {auth}/.well-known/jwks` | Public signing keys (how the API validates tokens) |

### Endpoints

| Endpoint | Purpose |
|---|---|
| `GET/POST {auth}/connect/authorize` | Starts sign-in. Redirects to the login page when there's no session. |
| `POST {auth}/connect/token` | Code redemption, refresh, and token exchange. |
| `GET/POST {auth}/connect/logout` | Ends the session. |
| `GET/POST {auth}/connect/userinfo` | Claims for the bearer token. |
| `POST {auth}/connect/revoke` | Revokes a token. |
| `GET {auth}/Account/Login`, `/Account/Register` | Server-rendered sign-in and registration. |

### Scopes

| Scope | Grants |
|---|---|
| `expense.read` | Read expense data |
| `expense.write` | Create/update/delete expense data |
| `auth.admin` | Auth019's user-administration API |

Plus the standard `openid`, `profile`, `email`, `roles`, `offline_access`.

### Sign-in flow (Authorization Code + PKCE)

The SPA is a **public client** (`expensetracker019-spa`) — no client secret; PKCE is required.

```
1. GET {auth}/connect/authorize
     ?client_id=expensetracker019-spa
     &response_type=code
     &redirect_uri={spa}/callback
     &scope=openid profile email roles offline_access expense.read expense.write auth.admin
     &code_challenge={S256(verifier)}
     &code_challenge_method=S256
   → redirects to /Account/Login when signed out, then back with ?code=...

2. POST {auth}/connect/token      (application/x-www-form-urlencoded)
     grant_type=authorization_code
     client_id=expensetracker019-spa
     code={code}
     redirect_uri={spa}/callback
     code_verifier={verifier}
   → { access_token, refresh_token, id_token, expires_in, scope }
```

Access tokens last 15 minutes; refresh tokens 30 days and **rotate** on use (a reused refresh token is rejected).

**Refresh:**
```
POST {auth}/connect/token
  grant_type=refresh_token
  client_id=expensetracker019-spa
  refresh_token={token}
```
Rejected with `invalid_grant` if the account has been deactivated.

### Impersonation (RFC 8693 token exchange)

Lets an admin obtain a **read-only** token acting as another user.

```
POST {auth}/connect/token
  grant_type=urn:ietf:params:oauth:grant-type:token-exchange
  client_id=expensetracker019-spa
  subject_token={admin's access token}
  subject_token_type=urn:ietf:params:oauth:token-type:access_token
  requested_subject={target user id}
```

The returned token has **`scope: expense.read` only**, **no roles**, an `imp_by` claim naming the admin, and **no refresh token**. It expires in 15 minutes and cannot be renewed.

Rejected (`invalid_request` / `invalid_grant`) when the caller isn't an admin, is already impersonating, or the target is themselves, another admin, or deactivated.

### Admin API

Requires the `Admin` role **and** the `auth.admin` scope — so an impersonation token cannot reach it.

| Method | Path | Notes |
|---|---|---|
| GET | `{auth}/api/admin/users` | `search` (email or name), `includeInactive`, `page`, `pageSize` |
| GET | `{auth}/api/admin/users/{id}` | One user |
| POST | `{auth}/api/admin/users/{id}/deactivate` | Blocks future sign-in and refresh; revokes stored tokens |
| POST | `{auth}/api/admin/users/{id}/reactivate` | Restores access |

Rows include `roles`, `isActive`, `lastLoginAtUtc`, `deactivatedAtUtc`, `createdAtUtc`. Admins cannot deactivate themselves.

---

## ExpenseTracker019.Api — resource server

Every endpoint needs `Authorization: Bearer {access_token}` and the `expense.read` scope. **Every write additionally needs `expense.write`** — enforced globally, which is what makes impersonation tokens read-only.

All data is scoped to the token's `sub`; another user's ids return 404, never their data.

### Settings

| Method | Path | Notes |
|---|---|---|
| GET | `/api/settings/month-cycle` | `{startDay, isConfigured}` |
| PUT | `/api/settings/month-cycle` | `{startDay}` (1–31) |

### Budget periods

| Method | Path | Notes |
|---|---|---|
| GET | `/api/budget-periods/current` | Period containing today |
| GET | `/api/budget-periods/relative/{offset}` | `-1` previous, `1` next |
| GET | `/api/budget-periods/{id}` | One period |
| GET | `/api/budget-periods` | All, newest first |

Returns `{id, startDate, endDate, label}` — e.g. `"25 Jul – 24 Aug 2026"`.

### Categories & Heads

| Method | Path | Notes |
|---|---|---|
| GET | `/api/categories?includeArchived=false` | Categories with nested heads |
| POST | `/api/categories` | `{name}`; 409 if the name is taken |
| PUT | `/api/categories/{id}` | Rename |
| DELETE | `/api/categories/{id}` | **Archives** (cascades to heads); data kept |
| POST | `/api/categories/{categoryId}/heads` | `{name}` |
| PUT | `/api/heads/{id}` | Rename |
| DELETE | `/api/heads/{id}` | **Archives**; data kept |

### Budgets

| Method | Path | Notes |
|---|---|---|
| GET | `/api/budget-periods/{periodId}/budgets` | Categories + heads with amounts and remaining allowance |
| PUT | `/api/budget-periods/{periodId}/categories/{categoryId}/budget` | `{amount}` |
| DELETE | `/api/budget-periods/{periodId}/categories/{categoryId}/budget` | Clears it **and its heads' budgets** for this period only |
| PUT | `/api/budget-periods/{periodId}/heads/{headId}/budget` | `{amount}`; enforces the category ceiling |
| DELETE | `/api/budget-periods/{periodId}/heads/{headId}/budget` | Clears this head's budget for this period |

All four return the full updated period budget, so no follow-up fetch is needed.

**Rejections to expect (400):** setting a head budget with no category budget; heads exceeding the category total (names the remaining allowance); lowering a category below its heads' sum (names the current total).

### Expenses

| Method | Path | Notes |
|---|---|---|
| GET | `/api/expenses` | `from`, `to`, `categoryId`, `headId`, `page`, `pageSize` (max 100) |
| POST | `/api/expenses` | `{headId, amount, expenseDate, note?}`; amount > 0 |
| PUT | `/api/expenses/{id}` | Same body |
| DELETE | `/api/expenses/{id}` | Hard delete |

Listing returns `{items, totalCount, totalAmount, page, pageSize}` and **includes expenses on archived heads** so history stays complete. Creating against an archived head returns 404.

### Reports

| Method | Path | Notes |
|---|---|---|
| GET | `/api/reports/summary?periodId={id}` | Budget vs actual for that period |
| GET | `/api/reports/summary/current` | Same for the current period |

Per category and head: `budget`, `spent`, `remaining`, `isOverBudget`, `isArchived`, plus period totals.
