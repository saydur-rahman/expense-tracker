import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export default function ImpersonationBanner() {
  const { isImpersonating, user, exitImpersonation } = useAuth()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  if (!isImpersonating) return null

  return (
    <div className="sticky top-0 z-20 flex flex-wrap items-center justify-between gap-2 bg-amber-500 px-4 py-2 text-sm text-amber-950">
      <span>
        Viewing as <strong>{user?.displayName}</strong> — read-only
      </span>
      <button
        onClick={() => {
          queryClient.clear()
          exitImpersonation()
          navigate('/admin/users')
        }}
        className="rounded bg-amber-950 px-3 py-1 text-xs font-medium text-amber-50"
      >
        Exit
      </button>
    </div>
  )
}
