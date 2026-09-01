import { fieldSm } from './ui'

/**
 * An arbitrary from/to range.
 *
 * **This is the one place the app's date scope is not the shared cycle.** Dashboard,
 * Budgets, Expenses and Income all agree on the month or week you are looking at, on
 * purpose — but a loan spans years and "which cycle was that payment in" is the wrong
 * question to have to answer. So loans and investments get a free range, and everything
 * else keeps the cycle.
 */
export default function DateRangePicker({
  from,
  to,
  onChange,
}: {
  from: string
  to: string
  onChange: (range: { from: string; to: string }) => void
}) {
  const active = from !== '' || to !== ''

  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="flex items-center gap-1.5 text-xs text-ink-muted">
        From
        <input
          type="date"
          value={from}
          // An empty end is open-ended, so only clamp when there is one to clamp to.
          max={to || undefined}
          onChange={(e) => onChange({ from: e.target.value, to })}
          className={fieldSm}
        />
      </label>

      <label className="flex items-center gap-1.5 text-xs text-ink-muted">
        To
        <input
          type="date"
          value={to}
          min={from || undefined}
          onChange={(e) => onChange({ from, to: e.target.value })}
          className={fieldSm}
        />
      </label>

      {active && (
        <button
          type="button"
          onClick={() => onChange({ from: '', to: '' })}
          className="rounded-lg px-2 py-1 text-xs text-ink-muted transition-colors hover:bg-raised hover:text-ink"
        >
          Clear
        </button>
      )}
    </div>
  )
}
