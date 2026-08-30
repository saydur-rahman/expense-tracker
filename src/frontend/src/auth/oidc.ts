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
