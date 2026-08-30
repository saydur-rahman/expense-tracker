import { useQuery } from '@tanstack/react-query'
import { budgetPeriodsApi } from '../api/settings'

interface PeriodPickerProps {
  label: string
  offset: number
  onOffsetChange: (offset: number) => void
}

/**
 * Moving between cycles. The arrows step one at a time and reach any cycle, including ones
 * never opened before; the dropdown jumps straight to one there is something to see in.
 *
 * The list is computed by the API rather than read from the BudgetPeriods table: rows are
 * only created when a cycle is actually visited, so a table-driven list would silently skip
 * months that hold expenses. It also creates nothing — merely listing history must not write.
 */
export default function PeriodPicker({ label, offset, onOffsetChange }: PeriodPickerProps) {
  const { data: windows } = useQuery({
    queryKey: ['period-windows'],
    queryFn: () => budgetPeriodsApi.recent(),
    staleTime: 5 * 60 * 1000,
  })

  // The arrows can walk past the ends of the list; keep the current place selectable so the
  // dropdown never shows a blank while you are somewhere it doesn't know about.
  const known = windows?.some((w) => w.offset === offset) ?? false

  return (
    <div className="flex items-center justify-between gap-2 rounded-xl border border-line bg-card p-1.5 shadow-sm">
      <Step direction="previous" onClick={() => onOffsetChange(offset - 1)} />

      {windows && windows.length > 1 ? (
        <label className="relative min-w-0 flex-1 text-center">
          <span className="sr-only">Choose a period</span>
          <select
            value={known ? offset : ''}
            onChange={(e) => onOffsetChange(Number(e.target.value))}
            className="w-full cursor-pointer appearance-none bg-transparent px-6 py-1 text-center text-sm font-medium text-ink focus:outline-none"
          >
            {!known && <option value="">{label}</option>}
            {windows.map((window) => (
              <option key={window.offset} value={window.offset}>
                {window.label}
                {window.offset === 0 ? ' · now' : ''}
              </option>
            ))}
          </select>
          <span
            aria-hidden="true"
            className="pointer-events-none absolute right-1 top-1/2 -translate-y-1/2 text-xs text-ink-muted"
          >
            ▾
          </span>
        </label>
      ) : (
        <span className="min-w-0 flex-1 truncate text-center text-sm font-medium text-ink">
          {label}
        </span>
      )}

      <Step direction="next" onClick={() => onOffsetChange(offset + 1)} />
    </div>
  )
}

function Step({ direction, onClick }: { direction: 'previous' | 'next'; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={`${direction === 'previous' ? 'Previous' : 'Next'} period`}
      className="grid size-9 shrink-0 place-items-center rounded-lg text-lg leading-none text-ink-muted transition-colors hover:bg-raised hover:text-brand-700 dark:hover:text-brand-300"
    >
      {direction === 'previous' ? '‹' : '›'}
    </button>
  )
}
