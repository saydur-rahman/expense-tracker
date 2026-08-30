import { useEffect } from 'react'
import { Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

export default function ProtectedRoute() {
  const { user, isLoading, login } = useAuth()

  useEffect(() => {
    // No local login form to send them to — sign-in happens on Auth019.
    if (!isLoading && !user) {
      login()
    }
  }, [isLoading, user, login])

  if (isLoading || !user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-card">
        <p className="text-ink-muted">Loading…</p>
      </div>
    )
  }

  return <Outlet />
}
