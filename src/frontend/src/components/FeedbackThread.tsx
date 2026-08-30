import { useState } from 'react'
import type { Feedback } from '../api/feedback'
import { statusClass, statusLabel } from '../api/feedback'

/**
 * The conversation itself. Shared by the user's view and the admin's so a thread
 * reads identically on both sides — only the reply handler differs.
 */
export default function FeedbackThread({
  feedback,
  onReply,
  isReplying,
  error,
}: {
  feedback: Feedback
  onReply: (body: string) => void
  isReplying: boolean
  error: string | null
}) {
  const [body, setBody] = useState('')

  return (
    <div className="flex flex-col gap-3">
      <ul className="flex flex-col gap-2">
        {feedback.messages.map((m) => (
          <li
            key={m.id}
            className={`max-w-[85%] rounded-xl border px-3 py-2 ${
              m.isFromAdmin
                ? 'self-start border-brand-200 bg-brand-50 dark:border-brand-900 dark:bg-brand-950'
                : 'self-end border-line bg-raised'
            }`}
          >
            <p className="whitespace-pre-wrap text-sm text-ink">{m.body}</p>
            <p className="mt-1 text-xs text-ink-muted">
              {m.isFromAdmin ? `${m.authorName} · support` : m.authorName} ·{' '}
              {new Date(m.createdAtUtc).toLocaleString()}
            </p>
          </li>
        ))}
      </ul>

      {feedback.canReply ? (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (body.trim()) {
              onReply(body.trim())
              setBody('')
            }
          }}
          className="flex flex-col gap-2 border-t border-line-soft pt-3"
        >
          <textarea
            value={body}
            onChange={(e) => setBody(e.target.value)}
            rows={3}
            maxLength={4000}
            placeholder="Write a reply…"
            className="w-full rounded-lg border border-line bg-input px-3 py-2.5 text-base text-ink placeholder:text-ink-muted transition-colors focus:border-brand-500 focus:outline-none"
          />
          {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}
          <button
            type="submit"
            disabled={isReplying || !body.trim()}
            className="self-start rounded-lg bg-brand-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-brand-500 dark:text-brand-950 dark:hover:bg-brand-400"
          >
            {isReplying ? 'Sending…' : 'Send reply'}
          </button>
        </form>
      ) : (
        <p className="rounded-lg border border-line bg-raised px-3 py-2.5 text-sm text-ink-muted">
          This feedback was closed
          {feedback.resolvedAtUtc ? ` on ${new Date(feedback.resolvedAtUtc).toLocaleDateString()}` : ''}
          , so it can't take any more replies. Submit new feedback if there's more to say.
        </p>
      )}
    </div>
  )
}

export function StatusBadge({ status }: { status: Feedback['status'] }) {
  return (
    <span className={`rounded-md px-2 py-0.5 text-xs font-medium ${statusClass[status]}`}>
      {statusLabel[status]}
    </span>
  )
}
