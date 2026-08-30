import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { feedbackApi } from '../../api/feedback'
import { ApiError } from '../../api/client'
import FeedbackThread, { StatusBadge } from '../../components/FeedbackThread'

export default function FeedbackPage() {
  const queryClient = useQueryClient()
  const [subject, setSubject] = useState('')
  const [message, setMessage] = useState('')
  const [openId, setOpenId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [replyError, setReplyError] = useState<string | null>(null)

  const { data: list, isLoading } = useQuery({
    queryKey: ['feedback', 'mine'],
    queryFn: feedbackApi.listMine,
  })

  const { data: thread } = useQuery({
    queryKey: ['feedback', 'mine', openId],
    queryFn: () => feedbackApi.getMine(openId!),
    enabled: !!openId,
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['feedback'] })

  const submit = useMutation({
    mutationFn: () => feedbackApi.submit(subject.trim(), message.trim()),
    onSuccess: (created) => {
      setSubject('')
      setMessage('')
      setError(null)
      setOpenId(created.id)
      refresh()
    },
    onError: (e) => setError(e instanceof ApiError ? e.message : 'Could not send your feedback.'),
  })

  const reply = useMutation({
    mutationFn: (body: string) => feedbackApi.reply(openId!, body),
    onSuccess: () => {
      setReplyError(null)
      refresh()
    },
    onError: (e) => setReplyError(e instanceof ApiError ? e.message : 'Could not send your reply.'),
  })

  return (
    <div className="flex flex-col gap-4">
      <form
        onSubmit={(e) => {
          e.preventDefault()
          if (subject.trim() && message.trim()) submit.mutate()
        }}
        className="flex flex-col gap-3 rounded-xl border border-line bg-card p-4 shadow-sm"
      >
        <div>
          <h2 className="font-medium text-ink">Send feedback</h2>
          <p className="text-sm text-ink-muted">
            Report a problem or suggest something. You'll see any reply here.
          </p>
        </div>

        <input
          value={subject}
          onChange={(e) => setSubject(e.target.value)}
          placeholder="Subject"
          maxLength={200}
          required
          className="w-full rounded-lg border border-line bg-input px-3 py-2.5 text-base text-ink placeholder:text-ink-muted transition-colors focus:border-brand-500 focus:outline-none"
        />
        <textarea
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          rows={4}
          maxLength={4000}
          placeholder="What would you like to tell us?"
          required
          className="w-full rounded-lg border border-line bg-input px-3 py-2.5 text-base text-ink placeholder:text-ink-muted transition-colors focus:border-brand-500 focus:outline-none"
        />

        {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}

        <button
          type="submit"
          disabled={submit.isPending}
          className="self-start rounded-lg bg-brand-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-brand-500 dark:text-brand-950 dark:hover:bg-brand-400"
        >
          {submit.isPending ? 'Sending…' : 'Send feedback'}
        </button>
      </form>

      <div>
        <h2 className="mb-2 font-medium text-ink">Your feedback</h2>

        {isLoading && <p className="text-ink-muted">Loading…</p>}

        {list?.length === 0 && (
          <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
            You haven't sent any feedback yet.
          </p>
        )}

        <div className="flex flex-col gap-2">
          {list?.map((f) => (
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
                <p className="mt-1 text-xs text-ink-muted">
                  {f.messageCount} {f.messageCount === 1 ? 'message' : 'messages'} · updated{' '}
                  {new Date(f.updatedAtUtc).toLocaleDateString()}
                </p>
              </button>

              {openId === f.id && thread && (
                <div className="border-t border-line-soft px-4 py-3">
                  <FeedbackThread
                    feedback={thread}
                    onReply={(body) => reply.mutate(body)}
                    isReplying={reply.isPending}
                    error={replyError}
                  />
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
