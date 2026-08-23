import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { User } from 'oidc-client-ts'
import {
  userManager,
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
  isImpersonating: boolean
  impersonatedBy: string | null
}

interface AuthContextValue {
  user: CurrentUser | null
  isLoading: boolean
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
    isImpersonating: impersonatedBy !== null,
    impersonatedBy,
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)

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

  const login = useCallback(() => userManager.signinRedirect(), [])

  const logout = useCallback(async () => {
    clearImpersonationToken()
    sessionStorage.removeItem('admin.session')
    await userManager.signoutRedirect()
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
      isAdmin: user?.roles.includes('Admin') ?? false,
      isImpersonating: user?.isImpersonating ?? false,
      login,
      logout,
      startImpersonation,
      exitImpersonation,
    }),
    [user, isLoading, login, logout, startImpersonation, exitImpersonation],
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
