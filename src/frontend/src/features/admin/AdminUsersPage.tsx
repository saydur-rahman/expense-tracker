import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { adminApi, type AdminUser } from '../../api/admin'
import { useAuth } from '../../auth/AuthContext'
import { ApiError } from '../../api/client'

export default function AdminUsersPage() {
  const [search, setSearch] = useState('')
  const [error, setError] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const { startImpersonation, user: currentUser } = useAuth()
  const navigate = useNavigate()

  const { data, isLoading } = useQuery({
    queryKey: ['admin-users', search],
    queryFn: () => adminApi.listUsers(search),
  })

  const setActive = useMutation({
    mutationFn: ({ id, activate }: { id: string; activate: boolean }) =>
      activate ? adminApi.reactivate(id) : adminApi.deactivate(id),
    onSuccess: () => {
      setError(null)
      queryClient.invalidateQueries({ queryKey: ['admin-users'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not update user.'),
  })

  const impersonate = useMutation({
    mutationFn: (id: string) => adminApi.impersonate(id),
    onSuccess: (accessToken) => {
      setError(null)
      queryClient.clear()
      startImpersonation(accessToken)
      navigate('/')
    },
    onError: (err) =>
      setError(
        err instanceof ApiError || err instanceof Error
          ? err.message
          : 'Could not start impersonation.',
      ),
  })

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-ink">Users</h1>
        <p className="text-sm text-ink-muted">
          {data?.totalCount ?? 0} account{data?.totalCount === 1 ? '' : 's'}
        </p>
      </div>

      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by email or name" className="rounded-lg border border-line px-3 py-2.5 text-base"
      />

      {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}
      {isLoading && <p className="text-ink-muted">Loading…</p>}

      <ul className="flex flex-col gap-2">
        {data?.items.map((user) => (
          <UserRow
            key={user.id}
            user={user}
            isSelf={user.id === currentUser?.id}
            onToggleActive={() => setActive.mutate({ id: user.id, activate: !user.isActive })}
            onImpersonate={() => impersonate.mutate(user.id)}
          />
        ))}
      </ul>

      {data?.items.length === 0 && (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          No users match that search.
        </p>
      )}
    </div>
  )
}

function UserRow({
  user,
  isSelf,
  onToggleActive,
  onImpersonate,
}: {
  user: AdminUser
  isSelf: boolean
  onToggleActive: () => void
  onImpersonate: () => void
}) {
  const isAdmin = user.roles.includes('Admin')

  return (
    <li className="rounded-xl border border-line bg-card p-3 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate font-medium text-ink">
            {user.displayName}
            {isAdmin && (
              <span className="ml-2 rounded-md bg-brand-100 px-1.5 py-0.5 text-xs font-medium text-brand-700 dark:bg-brand-950 dark:text-brand-300">
                Admin
              </span>
            )}
            {!user.isActive && (
              <span className="ml-2 rounded-md bg-negative-100 px-1.5 py-0.5 text-xs font-medium text-negative-700 dark:bg-negative-950 dark:text-negative-400">
                Deactivated
              </span>
            )}
          </p>
          <p className="truncate text-sm text-ink-muted">{user.email}</p>
          <p className="text-xs text-ink-muted">
            Last login: {user.lastLoginAtUtc ? new Date(user.lastLoginAtUtc).toLocaleString() : 'never'}
          </p>
        </div>

        <div className="flex shrink-0 gap-3">
          {!isSelf && !isAdmin && user.isActive && (
            <button onClick={onImpersonate} className="text-sm text-brand-600 dark:text-brand-400">
              View as
            </button>
          )}
          {!isSelf && (
            <button
              onClick={onToggleActive}
              className={`text-sm font-medium transition-colors ${user.isActive ? 'text-ink-muted hover:text-negative-600' : 'text-positive-600 hover:text-positive-700'}`}
            >
              {user.isActive ? 'Deactivate' : 'Reactivate'}
            </button>
          )}
        </div>
      </div>
    </li>
  )
}
