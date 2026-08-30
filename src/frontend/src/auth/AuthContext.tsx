import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { User } from 'oidc-client-ts'
import {
  userManager,
  currentReturnPath,
  stashAdminSession,
  takeAdminSession,
  hasAdminSession,
  setImpersonationToken,
  getImpersonationToken,
  clearImpersonationToken,
} from './oidc'
import { decodeJwt } from './jwt'

export interface CurrentUser {
  id: string
  email: string
  displayName: string
  roles: string[]
  scopes: string[]
  /** ISO 3166-1 alpha-2, or null for accounts that predate the country field. */
  country: string | null
  /** ISO 4217, derived by Auth019 from the country. Null when there is no country. */
  currency: string | null
  isImpersonating: boolean
  impersonatedBy: string | null
}

interface AuthContextValue {
  user: CurrentUser | null
  isLoading: boolean
  /** True from the moment sign-out starts until the browser leaves the page. */
  isSigningOut: boolean
  isAdmin: boolean
  isImpersonating: boolean
  login: () => Promise<void>
  logout: () => Promise<void>
  startImpersonation: (accessToken: string) => void
  exitImpersonation: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

function toArray(value: unknown): string[] {
  if (Array.isArray(value)) return value.map(String)
  if (typeof value === 'string') return value.split(' ').filter(Boolean)
  return []
}

/** Builds the current user from the access token actually being sent to the API. */
function readUser(accessToken: string): CurrentUser | null {
  const claims = decodeJwt(accessToken)
  if (!claims) return null

  const impersonatedBy = (claims.imp_by as string) ?? null

  return {
    id: String(claims.sub ?? ''),
    email: String(claims.email ?? ''),
    displayName: String(claims.name ?? claims.email ?? ''),
    // An impersonation token carries no roles, so admin UI can never appear
    // during an impersonated session.
    roles: toArray(claims.role),
    scopes: toArray(claims.scope),
    country: (claims.country as string) ?? null,
    currency: (claims.currency as string) ?? null,
    isImpersonating: impersonatedBy !== null,
    impersonatedBy,
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSigningOut, setIsSigningOut] = useState(false)

  const applyOidcUser = useCallback((oidcUser: User | null) => {
    if (!oidcUser?.access_token) {
      setUser(null)
      return
    }
    setUser(readUser(oidcUser.access_token))
  }, [])

  useEffect(() => {
    let cancelled = false

    async function bootstrap() {
      // An active impersonation token wins: it is what API calls actually use.
      const impersonationToken = getImpersonationToken()
      if (impersonationToken) {
        if (!cancelled) {
          setUser(readUser(impersonationToken))
          setIsLoading(false)
        }
        return
      }

      const oidcUser = await userManager.getUser()
      if (cancelled) return

      if (oidcUser && !oidcUser.expired) {
        applyOidcUser(oidcUser)
      } else {
        setUser(null)
      }
      setIsLoading(false)
    }

    bootstrap()

    // Impersonation overrides the OIDC session, so ignore these while it is active.
    const onLoaded = (u: User) => {
      if (!getImpersonationToken()) applyOidcUser(u)
    }
    const onUnloaded = () => {
      if (!getImpersonationToken()) setUser(null)
    }

    userManager.events.addUserLoaded(onLoaded)
    userManager.events.addUserUnloaded(onUnloaded)

    return () => {
      cancelled = true
      userManager.events.removeUserLoaded(onLoaded)
      userManager.events.removeUserUnloaded(onUnloaded)
    }
  }, [applyOidcUser])

  // Carry the route they were trying to reach through Auth019 and back, so a deep
  // link (or a bookmarked screen) doesn't dump them on the dashboard after signing in.
  const login = useCallback(
    () => userManager.signinRedirect({ state: currentReturnPath() }),
    [],
  )

  const logout = useCallback(async () => {
    // signoutRedirect() removes the stored user BEFORE it navigates, which fires
    // userUnloaded and empties this context. Without this flag, ProtectedRoute sees
    // a null user and starts a fresh sign-in — and that navigation wins the race,
    // going to /connect/authorize while the cookie is still valid and landing the
    // user straight back on the dashboard. Signing out appeared to do nothing.
    setIsSigningOut(true)
    clearImpersonationToken()
    sessionStorage.removeItem('admin.session')

    try {
      await userManager.signoutRedirect()
    } catch {
      // Never strand someone on a page that will bounce them back in.
      setIsSigningOut(false)
      window.location.assign('/signed-out')
    }
  }, [])

  const startImpersonation = useCallback((accessToken: string) => {
    userManager.getUser().then((adminUser) => {
      if (adminUser) stashAdminSession(adminUser)
      setImpersonationToken(accessToken)
      setUser(readUser(accessToken))
    })
  }, [])

  const exitImpersonation = useCallback(() => {
    clearImpersonationToken()
    const admin = takeAdminSession()
    if (admin?.access_token) {
      setUser(readUser(admin.access_token))
    } else {
      userManager.getUser().then(applyOidcUser)
    }
  }, [applyOidcUser])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      isSigningOut,
      isAdmin: user?.roles.includes('Admin') ?? false,
      isImpersonating: user?.isImpersonating ?? false,
      login,
      logout,
      startImpersonation,
      exitImpersonation,
    }),
    [user, isLoading, isSigningOut, login, logout, startImpersonation, exitImpersonation],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return ctx
}

export { hasAdminSession }
