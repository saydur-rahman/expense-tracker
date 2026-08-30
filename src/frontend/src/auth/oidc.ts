import { UserManager, WebStorageStateStore, type User } from 'oidc-client-ts'

const AUTH_BASE_URL = import.meta.env.VITE_AUTH_BASE_URL as string

/**
 * Authorization Code + PKCE against Auth019. The SPA is a public client, so it
 * holds no secret — PKCE is what proves the code redemption came from this app.
 * Credentials are only ever entered on Auth019's own pages, never here.
 */
export const userManager = new UserManager({
  authority: AUTH_BASE_URL,
  client_id: 'expensetracker019-spa',
  redirect_uri: `${window.location.origin}/callback`,
  silent_redirect_uri: `${window.location.origin}/silent-renew`,
  // Must be a PUBLIC route. Landing on a protected one immediately kicks off a
  // fresh sign-in, which Google answers silently — making logout look broken.
  post_logout_redirect_uri: `${window.location.origin}/signed-out`,
  response_type: 'code',
  scope: 'openid profile email roles offline_access expense.read expense.write auth.admin',
  automaticSilentRenew: true,
  // Tokens in sessionStorage rather than localStorage: they die with the tab,
  // which limits exposure if the machine is shared.
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
})

/**
 * Sign-in happens on Auth019 and comes back to `/callback`, so the route the user was
 * actually trying to reach has to survive the round trip. It rides in the OIDC `state`
 * parameter and is read back by the callback page.
 */
export function currentReturnPath(): string {
  return window.location.pathname + window.location.search + window.location.hash
}

/** Landing on one of these after sign-in would bounce or loop, so they never win. */
const AUTH_ROUTES = ['/callback', '/silent-renew', '/signed-out']

/**
 * The path to land on, given whatever came back in `state`. Validated rather than
 * trusted: it round-trips through a URL and browser storage, and both `//host` and
 * `/\host` are browser-legal ways of leaving the site entirely.
 */
export function safeReturnPath(state: unknown): string {
  // One leading slash and no second separator after it.
  if (typeof state !== 'string' || !/^\/(?![/\\])/.test(state)) {
    return '/'
  }

  const path = state.split(/[?#]/)[0]
  return AUTH_ROUTES.includes(path) ? '/' : state
}

/** Key under which an impersonating admin's own session is parked. */
const ADMIN_USER_KEY = 'admin.session'

export function stashAdminSession(user: User) {
  sessionStorage.setItem(ADMIN_USER_KEY, JSON.stringify(user))
}

export function takeAdminSession(): User | null {
  const raw = sessionStorage.getItem(ADMIN_USER_KEY)
  if (!raw) return null
  sessionStorage.removeItem(ADMIN_USER_KEY)
  try {
    return JSON.parse(raw) as User
  } catch {
    return null
  }
}

export function hasAdminSession() {
  return sessionStorage.getItem(ADMIN_USER_KEY) !== null
}

/** The access token currently used for API calls (impersonation token when active). */
const IMPERSONATION_TOKEN_KEY = 'impersonation.token'

export function setImpersonationToken(token: string) {
  sessionStorage.setItem(IMPERSONATION_TOKEN_KEY, token)
}

export function getImpersonationToken(): string | null {
  return sessionStorage.getItem(IMPERSONATION_TOKEN_KEY)
}

export function clearImpersonationToken() {
  sessionStorage.removeItem(IMPERSONATION_TOKEN_KEY)
}

export { AUTH_BASE_URL }
