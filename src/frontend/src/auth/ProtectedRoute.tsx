import { useEffect } from 'react'
import { Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

export default function ProtectedRoute() {
  const { user, isLoading, isSigningOut, login } = useAuth()

  useEffect(() => {
    // No local login form to send them to — sign-in happens on Auth019.
    // Never while signing out: the user is briefly null mid-sign-out, and starting
    // a sign-in there races the sign-out navigation and wins, undoing it.
    if (!isLoading && !user && !isSigningOut) {
      login()
    }
  }, [isLoading, user, isSigningOut, login])

  if (isLoading || !user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-card">
        <p className="text-ink-muted">{isSigningOut ? 'Signing out…' : 'Loading…'}</p>
      </div>
    )
  }

  return <Outlet />
}
