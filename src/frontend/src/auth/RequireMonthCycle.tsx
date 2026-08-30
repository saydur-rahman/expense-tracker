import { useQuery } from '@tanstack/react-query'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { settingsApi } from '../api/settings'
import { useAuth } from './AuthContext'

/**
 * Sends users who have never chosen a month cycle to onboarding first, since
 * every budget/report screen downstream depends on a resolved period.
 */
export default function RequireMonthCycle() {
  const location = useLocation()
  const { isImpersonating } = useAuth()
  const { data, isLoading } = useQuery({
    queryKey: ['month-cycle'],
    queryFn: settingsApi.getMonthCycle,
  })

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p className="text-ink-muted">Loading…</p>
      </div>
    )
  }

  // Never force onboarding on an impersonating admin: the setup form is a write,
  // which read-only impersonation blocks, so redirecting would trap them.
  const isOnboarding = location.pathname === '/settings/month-cycle'
  if (!data?.isConfigured && !isOnboarding && !isImpersonating) {
    return <Navigate to="/settings/month-cycle"replace />
  }

  return <Outlet />
}
