import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

export default function ProtectedRoute() {
  const { user, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-white dark:bg-gray-950">
        <p className="text-gray-500 dark:text-gray-400">Loading…</p>
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}
