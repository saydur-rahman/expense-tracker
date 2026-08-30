import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export default function ImpersonationBanner() {
  const { isImpersonating, user, exitImpersonation } = useAuth()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  if (!isImpersonating) return null

  // Deliberately the loudest thing on screen: an admin must never forget they are
  // looking at someone else's books.
  return (
    <div className="sticky top-0 z-30 flex flex-wrap items-center justify-between gap-2 bg-negative-600 px-4 py-2 text-sm text-white">
      <span className="flex items-center gap-2">
        <span aria-hidden="true" className="size-2 rounded-full bg-white/80" />
        Viewing as <strong className="font-semibold">{user?.displayName}</strong> — read-only
      </span>
      <button
        onClick={() => {
          queryClient.clear()
          exitImpersonation()
          navigate('/admin/users')
        }}
        className="rounded-lg bg-white/15 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-white/25"
      >
        Exit
      </button>
    </div>
  )
}
