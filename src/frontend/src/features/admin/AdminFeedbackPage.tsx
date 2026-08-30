import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminFeedbackApi, type FeedbackStatus, statusLabel } from '../../api/feedback'
import { ApiError } from '../../api/client'
import FeedbackThread, { StatusBadge } from '../../components/FeedbackThread'
import AdminTabs from './AdminTabs'

const filters: Array<{ value: FeedbackStatus | 'All'; label: string }> = [
  { value: 'All', label: 'All' },
  { value: 'Open', label: 'Open' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'Resolved', label: 'Resolved' },
]

export default function AdminFeedbackPage() {
  const queryClient = useQueryClient()
  const [filter, setFilter] = useState<FeedbackStatus | 'All'>('All')
  const [openId, setOpenId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['admin-feedback', filter],
    queryFn: () => adminFeedbackApi.list(filter === 'All' ? undefined : filter),
  })

  const { data: thread } = useQuery({
    queryKey: ['admin-feedback', 'thread', openId],
    queryFn: () => adminFeedbackApi.get(openId!),
    enabled: !!openId,
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin-feedback'] })

  const reply = useMutation({
    mutationFn: (body: string) => adminFeedbackApi.reply(openId!, body),
    onSuccess: () => { setError(null); refresh() },
    onError: (e) => setError(e instanceof ApiError ? e.message : 'Could not send the reply.'),
  })

  const setStatus = useMutation({
    mutationFn: ({ id, status }: { id: string; status: FeedbackStatus }) =>
      adminFeedbackApi.setStatus(id, status),
    onSuccess: () => { setError(null); refresh() },
    onError: (e) => setError(e instanceof ApiError ? e.message : 'Could not change the status.'),
  })

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-ink">Feedback</h1>
        <p className="text-sm text-ink-muted">
          {data ? `${data.openCount} open · ${data.inProgressCount} in progress · ${data.totalCount} total` : ' '}
        </p>
      </div>

      <AdminTabs />

      <div className="flex gap-1 rounded-xl border border-line bg-raised p-1">
        {filters.map((f) => (
          <button
            key={f.value}
            type="button"
            onClick={() => setFilter(f.value)}
            className={`flex-1 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
              filter === f.value
                ? 'bg-card text-brand-700 shadow-sm dark:text-brand-300'
                : 'text-ink-muted hover:text-ink'
            }`}
          >
            {f.label}
          </button>
        ))}
      </div>

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      {data?.items.length === 0 && (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          No feedback {filter === 'All' ? 'yet' : `with status “${statusLabel[filter as FeedbackStatus]}”`}.
        </p>
      )}

      <div className="flex flex-col gap-2">
        {data?.items.map((f) => (
          <div key={f.id} className="overflow-hidden rounded-xl border border-line bg-card shadow-sm">
            <button
              type="button"
              onClick={() => setOpenId(openId === f.id ? null : f.id)}
              aria-expanded={openId === f.id}
              className="w-full px-4 py-3 text-left transition-colors hover:bg-raised"
            >
              <div className="flex items-baseline justify-between gap-2">
                <span className="truncate font-medium text-ink">{f.subject}</span>
                <StatusBadge status={f.status} />
              </div>
              <p className="mt-1 truncate text-xs text-ink-muted">
                {f.submittedByName} · {f.submittedByEmail} · {f.messageCount}{' '}
                {f.messageCount === 1 ? 'message' : 'messages'} · updated{' '}
                {new Date(f.updatedAtUtc).toLocaleDateString()}
              </p>
            </button>

            {openId === f.id && thread && (
              <div className="border-t border-line-soft px-4 py-3">
                <div className="mb-3 flex flex-wrap items-center gap-2">
                  <span className="text-xs font-medium uppercase tracking-wide text-ink-muted">
                    Set status
                  </span>
                  {(['Open', 'InProgress', 'Resolved'] as FeedbackStatus[]).map((s) => (
                    <button
                      key={s}
                      type="button"
                      disabled={setStatus.isPending || thread.status === s}
                      onClick={() => setStatus.mutate({ id: f.id, status: s })}
                      className={`rounded-lg border px-2.5 py-1 text-xs font-medium transition-colors disabled:cursor-not-allowed ${
                        thread.status === s
                          ? 'border-brand-600 bg-brand-600 text-white dark:border-brand-500 dark:bg-brand-500 dark:text-brand-950'
                          : 'border-line bg-card text-ink-soft hover:bg-raised'
                      }`}
                    >
                      {statusLabel[s]}
                    </button>
                  ))}
                </div>

                <FeedbackThread
                  feedback={thread}
                  onReply={(body) => reply.mutate(body)}
                  isReplying={reply.isPending}
                  error={error}
                />
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
