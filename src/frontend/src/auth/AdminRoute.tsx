import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

export default function AdminRoute() {
  const { isAdmin, isLoading } = useAuth()

  if (isLoading) {
    return <p className="p-6 text-ink-muted">Loading…</p>
  }

  return isAdmin ? <Outlet /> : <Navigate to="/"replace />
}
