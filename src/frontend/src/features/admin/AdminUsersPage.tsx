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
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">Users</h1>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          {data?.totalCount ?? 0} account{data?.totalCount === 1 ? '' : 's'}
        </p>
      </div>

      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by email or name"
        className="rounded-lg border border-gray-300 px-3 py-2.5 text-base dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
      />

      {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
      {isLoading && <p className="text-gray-500">Loading…</p>}

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
        <p className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-400 dark:border-gray-700">
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
    <li className="rounded-lg border border-gray-200 bg-white p-3 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate font-medium text-gray-900 dark:text-gray-100">
            {user.displayName}
            {isAdmin && (
              <span className="ml-2 rounded bg-indigo-100 px-1.5 py-0.5 text-xs font-normal text-indigo-700 dark:bg-indigo-900 dark:text-indigo-300">
                Admin
              </span>
            )}
            {!user.isActive && (
              <span className="ml-2 rounded bg-red-100 px-1.5 py-0.5 text-xs font-normal text-red-700 dark:bg-red-900 dark:text-red-300">
                Deactivated
              </span>
            )}
          </p>
          <p className="truncate text-sm text-gray-500 dark:text-gray-400">{user.email}</p>
          <p className="text-xs text-gray-400">
            Last login: {user.lastLoginAtUtc ? new Date(user.lastLoginAtUtc).toLocaleString() : 'never'}
          </p>
        </div>

        <div className="flex shrink-0 gap-3">
          {!isSelf && !isAdmin && user.isActive && (
            <button onClick={onImpersonate} className="text-sm text-indigo-600 dark:text-indigo-400">
              View as
            </button>
          )}
          {!isSelf && (
            <button
              onClick={onToggleActive}
              className={`text-sm ${user.isActive ? 'text-gray-400 hover:text-red-600' : 'text-green-600'}`}
            >
              {user.isActive ? 'Deactivate' : 'Reactivate'}
            </button>
          )}
        </div>
      </div>
    </li>
  )
}
