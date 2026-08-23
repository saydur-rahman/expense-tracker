---
name: api-smoke-test
description: Sanity-check the expensetracker019 services — the OAuth flows on Auth019 and the expense API's endpoints and scope enforcement — without needing a browser. Use after backend changes.
---

# api-smoke-test

Start the stack first (`run-dev` skill) and read the Auth019 and expense-API URLs off the Aspire dashboard; ports are dynamic.

On Windows use PowerShell's `Invoke-RestMethod` — `curl` mangles JSON bodies and hides error payloads here.

## 1. Auth019 is a working OAuth server

- `GET {auth}/.well-known/openid-configuration` returns 200 with `issuer`, `token_endpoint`, `jwks_uri`, `grant_types_supported` (must include `authorization_code`, `refresh_token`, and `urn:ietf:params:oauth:grant-type:token-exchange`), and `code_challenge_methods_supported` containing `S256`.
- `GET {auth}/.well-known/jwks` returns at least one key.
- `GET {auth}/Account/Login` renders a sign-in form.
- `GET {auth}/connect/authorize?...` with no session returns 302 to `/Account/Login`.

## 2. Full Authorization Code + PKCE flow

Generate a verifier/challenge pair, sign in on `/Account/Login` (keep the cookie session), call `/connect/authorize`, capture the `code` from the redirect, then POST to `/connect/token` with `grant_type=authorization_code` and the `code_verifier`.

Expect an access token, refresh token, and id token. Decode the access token and confirm `sub`, `email`, `role`, `scope`, `aud` (includes `expensetracker019-api`), and `iss`.

Then `POST /connect/token` with `grant_type=refresh_token` — it should succeed **and rotate** the refresh token (reusing the old one must fail).

## 3. Cross-service validation

With that access token, `GET {api}/api/categories` must return 200. Without a token, 401. This is the real check that the resource server trusts Auth019's keys — if the issuer is misconfigured you'll get `invalid_token` in the `WWW-Authenticate` header.

## 4. Domain rules

- Set a month cycle, then `GET {api}/api/budget-periods/current` and confirm the boundaries match.
- Create a category and heads; `DELETE` one and confirm it vanishes from `GET /api/categories` but is present with `?includeArchived=true`, and that its expenses still appear in history and reports.
- **Budget constraint**: a head budget with no category budget → rejected; heads summing exactly to the total → allowed; over by 0.01 → rejected naming the remaining allowance; lowering the category below the head sum → rejected.
- Log expenses, then `GET /api/reports/summary/current` and check the arithmetic and over-budget flags.

## 5. Security boundaries (the important ones)

- A non-admin calling `{auth}/api/admin/users` → 403.
- **Token exchange**: `POST {auth}/connect/token` with `grant_type=urn:ietf:params:oauth:grant-type:token-exchange`, `subject_token={admin token}`, `requested_subject={user id}`. The result must have `scope: expense.read` only, **no roles**, an `imp_by` claim, and **no refresh token**.
- With that token: `GET {api}/...` allowed; every `POST`/`PUT`/`DELETE` → 403; `{auth}/api/admin/users` → 403; exchanging again → rejected.
- Exchange targeting yourself, another admin, or a deactivated user → rejected.
- Deactivate a user, then their `refresh_token` grant → `invalid_grant`. Reactivate and confirm it works again.

## Notes

- Run the subset relevant to what changed rather than everything each time.
- Register throwaway users for tests that mutate state so runs stay independent.
- To flip `IsActive` directly in SQL for a test, `sqlcmd` needs `-I` — filtered indexes on `AspNetUsers` otherwise make the `UPDATE` fail while appearing to succeed.
