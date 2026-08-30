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

Access tokens also carry `country` (ISO 3166-1 alpha-2) and `currency` (ISO 4217, derived from the country) when the account has a country set. The SPA formats every amount from the `currency` claim; accounts predating the country field carry neither and fall back to plain grouped numbers.

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

### Profile API

The signed-in user's own record. Lives here because Auth019 owns user data.

| Method | Path | Notes |
|---|---|---|
| GET | `{auth}/api/profile` | `{id, email, displayName, mobileNumber, country, countryName, currencyCode, hasPassword}` |
| PUT | `{auth}/api/profile` | `{displayName, mobileNumber, country}` — **email is not editable**; 400 on an unknown country |
| PUT | `{auth}/api/profile/password` | `{newPassword, confirmPassword}` → 204; 400 on a mismatch, a password Identity rejects, or an account with no password |
| GET | `{auth}/api/profile/countries` | The 244 country options, each with the currency it implies |

Any valid token may **read** a profile. An **impersonated** token (one carrying `imp_by`) is refused on both `PUT` routes with 403 — impersonation is read-only everywhere, and editing someone's profile while wearing their identity would be the loudest possible breach of that.

Changing the country changes the currency, but the currency travels on the access token, so the app must obtain a fresh token (a silent renew) before the new currency shows up.

The password route asks for **no current password**: the bearer token is the proof of identity. It **only ever replaces** a password, never grants one — an account with `hasPassword: false` (Google-only) is refused, and the profile screen hides the card for it entirely. Their credential lives at Google; this is not a route to acquiring a local one. Linking Google to an account that already has a password leaves the password intact, so those users keep the route. Strength is Identity's to judge, so the message on a rejected password matches what registration would have said.

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
| GET | `/api/settings/month-cycle` | `{periodKind, startDay, weekStartsOn, isConfigured}` |
| PUT | `/api/settings/month-cycle` | `{periodKind, startDay, weekStartsOn}` — `periodKind` is `Month` or `Week` |

`periodKind` picks the rhythm; only the field governing it is validated (`startDay` 1–31 for
`Month`, `weekStartsOn` a `DayOfWeek` name for `Week`). **Send both regardless** — the unused
one is stored as-is, so switching to weekly and back keeps the day of the month already chosen.

The route is still `month-cycle` for compatibility with existing links; it now governs both
rhythms. Settings are append-only and effective-dated, so switching never rewrites history.

### Budget periods

| Method | Path | Notes |
|---|---|---|
| GET | `/api/budget-periods/current` | Period containing today. Carries `kind` (`Month`/`Week`) alongside `startDate`, `endDate`, `label` |
| GET | `/api/budget-periods/relative/{offset}` | `-1` previous, `1` next |
| GET | `/api/budget-periods/{id}` | One period |
| GET | `/api/budget-periods` | All, newest first |

Returns `{id, startDate, endDate, label}` — e.g. `"25 Jul – 24 Aug 2026"`.

### Categories & Heads

| Method | Path | Notes |
|---|---|---|
| GET | `/api/categories?kind=Expense&includeArchived=false` | Categories with nested heads. `kind` is `Expense` (default) or `Income` |
| POST | `/api/categories` | `{name, kind?}`; 409 if the name is taken **within that kind** |
| PUT | `/api/categories/{id}` | Rename |
| DELETE | `/api/categories/{id}` | **Archives** (cascades to heads); data kept |
| POST | `/api/categories/{categoryId}/heads` | `{name}` |
| PUT | `/api/heads/{id}` | Rename |
| DELETE | `/api/heads/{id}` | **Archives**; data kept |

Categories carry a `kind` of `Expense` or `Income` (serialised as a string). The two are separate trees with the same shape; heads inherit their category's kind. A name may be reused across kinds — "Other" can sit in both.

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

Listing returns `{items, totalCount, totalAmount, page, pageSize}` and **includes expenses on archived heads** so history stays complete. Creating against an archived head returns 404, and against a head on an **income** category returns 400.

### Incomes

A mirror of expenses, against heads of `Income` categories. No budgets apply.

| Method | Path | Notes |
|---|---|---|
| GET | `/api/incomes` | `from`, `to`, `categoryId`, `headId`, `page`, `pageSize` (max 100) |
| POST | `/api/incomes` | `{headId, amount, incomeDate, note?}`; amount > 0 |
| PUT | `/api/incomes/{id}` | Same body |
| DELETE | `/api/incomes/{id}` | Hard delete |

Same list shape as expenses. Posting against a head on an **expense** category returns 400.

### Feedback

A user's own conversations with the admins. Scoped by the `sub` claim, so another user's id reads as 404 rather than leaking that it exists.

| Method | Path | Notes |
|---|---|---|
| GET | `/api/feedback` | Your threads, newest activity first (no messages) |
| GET | `/api/feedback/{id}` | One thread with its messages |
| POST | `/api/feedback` | `{subject, message}` — opens a thread with the first message |
| POST | `/api/feedback/{id}/replies` | `{body}`; **400 once resolved** |

### Admin feedback

Everyone's feedback. Requires the **Admin role** — the only place this service deliberately reads across users. An impersonation token carries no roles, so an impersonated session can't reach it.

| Method | Path | Notes |
|---|---|---|
| GET | `/api/admin/feedback?status=` | All threads; optional `Open` / `InProgress` / `Resolved`, plus open and in-progress counts |
| GET | `/api/admin/feedback/{id}` | One thread |
| POST | `/api/admin/feedback/{id}/replies` | `{body}`; an `Open` thread moves to `InProgress` automatically; **400 once resolved** |
| PUT | `/api/admin/feedback/{id}/status` | `{status}` — setting `Resolved` closes it and stamps `resolvedAtUtc` |

**Resolved means closed to everyone**, enforced in `FeedbackService` rather than either controller so neither side can post to a closed thread. An admin can reopen by setting the status back, which allows replies again.

### Reports

| Method | Path | Notes |
|---|---|---|
| GET | `/api/reports/summary?periodId={id}` | Budget vs actual for that period |
| GET | `/api/reports/summary/current` | Same for the current period |

Per category and head: `budget`, `spent`, `remaining`, `isOverBudget`, `isArchived`, plus period totals.

The summary carries **both ledgers**: `categories` is the spending breakdown, `incomeCategories` the income one — the dashboard's two tabs. Totals are `totalBudget`, `totalSpent`, `totalRemaining`, `totalIncome`, and `totalSaved` (income minus spending; negative means you spent more than you earned). On an income category the `spent` field carries the amount received and the budget fields stay null, so one component renders either tab.

**Budget rejections (400):** an income category or head can never take a budget.
