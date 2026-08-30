import type { CategoryKind } from '../api/categories'

/**
 * The Expense / Income switch. Used on the dashboard and the categories screen so
 * the two ledgers are toggled the same way everywhere.
 */
export default function LedgerTabs({
  value,
  onChange,
  labels,
}: {
  value: CategoryKind
  onChange: (kind: CategoryKind) => void
  labels?: Partial<Record<CategoryKind, string>>
}) {
  const options: CategoryKind[] = ['Expense', 'Income']

  return (
    <div
      role="tablist" aria-label="Ledger" className="flex gap-1 rounded-xl border border-line bg-raised p-1"
    >
      {options.map((option) => {
        const isActive = option === value
        return (
          <button
            key={option}
            type="button" role="tab" aria-selected={isActive}
            onClick={() => onChange(option)}
            className={`flex-1 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
 isActive
                ? 'bg-card text-brand-700 shadow-sm dark:text-brand-300'
                : 'text-ink-muted hover:text-ink'
            }`}
          >
            {labels?.[option] ?? option}
          </button>
        )
      })}
    </div>
  )
}
