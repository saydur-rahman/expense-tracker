import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { authApi, type AuthResponse, type UserDto } from '../api/auth'

interface AuthContextValue {
  user: UserDto | null
  isLoading: boolean
  isAdmin: boolean
  isImpersonating: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string) => Promise<void>
  loginWithGoogle: (idToken: string) => Promise<void>
  startImpersonation: (accessToken: string) => Promise<void>
  exitImpersonation: () => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

const ADMIN_TOKEN_KEY = 'adminAccessToken'

function persistSession(response: AuthResponse) {
  localStorage.setItem('accessToken', response.accessToken)
  localStorage.setItem('refreshToken', response.refreshToken)
}

function clearSession() {
  localStorage.removeItem('accessToken')
  localStorage.removeItem('refreshToken')
  localStorage.removeItem(ADMIN_TOKEN_KEY)
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    if (!localStorage.getItem('accessToken')) {
      setIsLoading(false)
      return
    }

    authApi
      .me()
      .then(setUser)
      .catch(() => {
        clearSession()
        setUser(null)
      })
      .finally(() => setIsLoading(false))
  }, [])

  async function login(email: string, password: string) {
    const response = await authApi.login({ email, password })
    persistSession(response)
    setUser(response.user)
  }

  async function register(email: string, password: string, displayName: string) {
    const response = await authApi.register({ email, password, displayName })
    persistSession(response)
    setUser(response.user)
  }

  async function loginWithGoogle(idToken: string) {
    const response = await authApi.google(idToken)
    persistSession(response)
    setUser(response.user)
  }

  async function startImpersonation(accessToken: string) {
    // Keep the admin's own token aside so exiting doesn't require signing in again.
    const adminToken = localStorage.getItem('accessToken')
    if (adminToken) localStorage.setItem(ADMIN_TOKEN_KEY, adminToken)

    localStorage.setItem('accessToken', accessToken)
    setUser(await authApi.me())
  }

  async function exitImpersonation() {
    const adminToken = localStorage.getItem(ADMIN_TOKEN_KEY)
    if (!adminToken) {
      logout()
      return
    }

    localStorage.setItem('accessToken', adminToken)
    localStorage.removeItem(ADMIN_TOKEN_KEY)
    setUser(await authApi.me())
  }

  function logout() {
    clearSession()
    setUser(null)
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        isAdmin: user?.roles.includes('Admin') ?? false,
        isImpersonating: user?.isImpersonating ?? false,
        login,
        register,
        loginWithGoogle,
        startImpersonation,
        exitImpersonation,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return ctx
}
