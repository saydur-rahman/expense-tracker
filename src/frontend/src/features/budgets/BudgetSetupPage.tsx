import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { budgetPeriodsApi, type PeriodKind } from '../../api/settings'
import { budgetsApi, type CategoryBudget, type PeriodBudgets } from '../../api/budgets'
import { ApiError } from '../../api/client'
import PeriodPicker from '../../components/PeriodPicker'
import { useMoney } from '../../lib/money'
import { readAmount } from '../../lib/calc'
import { AmountHint } from '../../components/AmountField'

export default function BudgetSetupPage() {
  const [offset, setOffset] = useState(0)
  const [search, setSearch] = useState('')

  const { data: period } = useQuery({
    queryKey: ['budget-period', offset],
    queryFn: () => budgetPeriodsApi.relative(offset),
  })

  const { data: budgets, isLoading } = useQuery({
    queryKey: ['budgets', period?.id],
    queryFn: () => budgetsApi.get(period!.id),
    enabled: !!period,
  })

  const query = search.trim().toLowerCase()
  const visibleCategories =
    budgets?.categories.filter(
      (c) =>
        !query ||
        c.categoryName.toLowerCase().includes(query) ||
        c.heads.some((h) => h.headName.toLowerCase().includes(query)),
    ) ?? []

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-ink">Budgets</h1>
        <p className="text-sm text-ink-muted">
          Put a figure on each head — the category is what they add up to.
        </p>
      </div>

      <PeriodPicker label={period?.label ?? '…'} offset={offset} onOffsetChange={setOffset} />

      {budgets && <IncomeAgainstBudget budgets={budgets} kind={period?.kind ?? 'Month'} />}

      {(budgets?.categories.length ?? 0) > 4 && (
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search categories and heads…"
          className="w-full rounded-lg border border-line bg-input px-3 py-2.5 text-base text-ink placeholder:text-ink-muted transition-colors focus:border-brand-500 focus:outline-none"
        />
      )}

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      {budgets?.categories.length === 0 && (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          Add a category first, then you can budget for it here.
        </p>
      )}

      <div className="flex flex-col gap-2">
        {visibleCategories.map((category) => (
          <CategoryBudgetCard
            key={category.categoryId}
            periodId={budgets!.periodId}
            category={category}
            // A handful of categories is nothing to tidy away; past that, start collapsed
            // so the whole period fits on one screen. Searching always opens the matches.
            defaultOpen={visibleCategories.length <= 4 || query.length > 0}
          />
        ))}
      </div>

      {budgets && budgets.categories.length > 0 && visibleCategories.length === 0 && (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          Nothing matches “{search}”.
        </p>
      )}
    </div>
  )
}

/**
 * What there is to divide up, and what is left after everything budgeted so far — so the
 * decisions below are made against the income for the same period rather than from memory.
 *
 * Over-budgeting is shown, not prevented: going past your income is a thing people
 * genuinely do, and hiding it would not make it less true.
 */
function IncomeAgainstBudget({ budgets, kind }: { budgets: PeriodBudgets; kind: PeriodKind }) {
  const money = useMoney()
  const left = budgets.totalIncome - budgets.totalBudgeted
  const isOver = left < 0
  const span = kind === 'Week' ? 'this week' : 'this month'

  return (
    <div className="rounded-xl border border-line bg-card p-4 shadow-sm">
      <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2">
        <span>
          <span className="block text-xs font-medium uppercase tracking-wide text-ink-muted">
            Income {span}
          </span>
          <span className="block text-lg font-semibold tabular-nums text-ink">
            {money.format(budgets.totalIncome)}
          </span>
        </span>

        <span className="text-right">
          <span className="block text-xs font-medium uppercase tracking-wide text-ink-muted">
            Budgeted
          </span>
          <span className="block text-lg font-semibold tabular-nums text-ink">
            {money.format(budgets.totalBudgeted)}
          </span>
        </span>

        <span className="text-right">
          <span className="block text-xs font-medium uppercase tracking-wide text-ink-muted">
            {isOver ? 'Over by' : 'Left to budget'}
          </span>
          <span
            className={`block text-lg font-semibold tabular-nums ${
              isOver
                ? 'text-negative-600 dark:text-negative-400'
                : 'text-positive-700 dark:text-positive-400'
            }`}
          >
            {money.format(left)}
          </span>
        </span>
      </div>

      {isOver && (
        <p className="mt-2 text-xs text-negative-600 dark:text-negative-400">
          You have budgeted {money.format(Math.abs(left))} more than you earned {span}.
        </p>
      )}

      {budgets.totalIncome === 0 && (
        <p className="mt-2 text-xs text-ink-muted">
          No income logged for this period yet — log it on the Income screen and this fills in.
        </p>
      )}
    </div>
  )
}

function CategoryBudgetCard({
  periodId,
  category,
  defaultOpen,
}: {
  periodId: string
  category: CategoryBudget
  defaultOpen: boolean
}) {
  const queryClient = useQueryClient()
  const money = useMoney()
  const [error, setError] = useState<string | null>(null)
  const [open, setOpen] = useState(defaultOpen)

  const mutation = useMutation({
    mutationFn: (fn: () => Promise<unknown>) => fn(),
    onSuccess: () => {
      setError(null)
      queryClient.invalidateQueries({ queryKey: ['budgets'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not save.'),
  })

  const headCount = category.heads.length
  const budgetedHeads = category.heads.filter((h) => h.amount !== null).length

  return (
    <div className="overflow-hidden rounded-xl border border-line bg-card shadow-sm">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className="w-full px-4 py-3 text-left transition-colors hover:bg-raised"
      >
        <div className="flex items-baseline justify-between gap-2">
          <span className="flex min-w-0 items-baseline gap-2">
            <span
              aria-hidden="true"
              className={`shrink-0 text-xs text-ink-muted transition-transform ${open ? 'rotate-90' : ''}`}
            >
              ▶
            </span>
            <span className="truncate font-medium text-ink">{category.categoryName}</span>
          </span>
          <span className="shrink-0 text-sm font-medium tabular-nums text-ink">
            {category.amount === null ? (
              <span className="font-normal text-ink-muted">Not budgeted</span>
            ) : (
              money.format(category.amount)
            )}
          </span>
        </div>

        {!open && (
          <p className="mt-1 flex flex-wrap items-center gap-x-2 text-xs text-ink-muted">
            <span>
              {budgetedHeads} of {headCount} {headCount === 1 ? 'head' : 'heads'} budgeted
            </span>
            <TargetNote category={category} compact />
          </p>
        )}
      </button>

      {open && (
        <div className="border-t border-line-soft px-4 py-3">
          <ul className="flex flex-col gap-2">
            {category.heads.map((head) => (
              <li key={head.headId} className="flex items-center justify-between gap-3">
                <span className="truncate text-sm text-ink-soft">{head.headName}</span>
                <AmountInput
                  value={head.amount}
                  placeholder="—"
                  onCommit={(amount) =>
                    mutation.mutate(() =>
                      amount === null
                        ? budgetsApi.clearHead(periodId, head.headId)
                        : budgetsApi.setHead(periodId, head.headId, amount),
                    )
                  }
                />
              </li>
            ))}
            {headCount === 0 && (
              <li className="text-xs text-ink-muted">No heads in this category yet.</li>
            )}
          </ul>

          {budgetedHeads > 0 && (
            <div className="mt-3 flex items-center justify-between gap-3 border-t border-line-soft pt-3 text-sm">
              <span className="text-ink-soft">Heads total</span>
              <span className="w-28 pr-2 text-right font-medium tabular-nums text-ink">
                {money.format(category.allocatedToHeads)}
              </span>
            </div>
          )}

          <div className="mt-3 flex items-center justify-between gap-3 border-t border-line-soft pt-3">
            <span className="min-w-0">
              <span className="block text-sm text-ink-soft">Target</span>
              <span className="block text-xs text-ink-muted">
                Optional — what you meant to spend here
              </span>
            </span>
            <AmountInput
              value={category.target}
              placeholder="—"
              onCommit={(amount) =>
                mutation.mutate(() =>
                  amount === null
                    ? budgetsApi.clearCategory(periodId, category.categoryId)
                    : budgetsApi.setCategory(periodId, category.categoryId, amount),
                )
              }
            />
          </div>

          <TargetNote category={category} />

          {error && <p className="mt-2 text-sm text-negative-600 dark:text-negative-400">{error}</p>}
        </div>
      )}
    </div>
  )
}

/**
 * How the heads compare with the target. Nothing to say unless both exist — a target on
 * its own is simply the budget, and heads on their own are answerable to nothing.
 */
function TargetNote({ category, compact }: { category: CategoryBudget; compact?: boolean }) {
  const money = useMoney()
  const difference = category.difference

  if (difference === null || category.target === null) return null

  const className = compact ? 'text-xs' : 'mt-2 block text-xs'

  if (difference === 0) {
    return (
      <span className={`${className} text-positive-700 dark:text-positive-400`}>
        Matches your {money.format(category.target)} target.
      </span>
    )
  }

  const over = difference > 0
  return (
    <span className={`${className} ${over ? 'text-ink-soft' : 'text-ink-soft'}`}>
      <strong className={`font-medium ${over ? 'text-negative-600 dark:text-negative-400' : 'text-brand-700 dark:text-brand-300'}`}>
        {money.format(Math.abs(difference))} {over ? 'extra' : 'short'}
      </strong>{' '}
      {over ? 'over' : 'of'} your {money.format(category.target)} target
    </span>
  )
}

/**
  * Commits on blur. Accepts arithmetic — see `lib/calc` — so a budget can be typed as
  * `635*3`; the hint underneath shows what that comes to before you leave the field.
  */
function AmountInput({
  value,
  placeholder,
  onCommit,
}: {
  value: number | null
  placeholder: string
  onCommit: (amount: number | null) => void
}) {
  const [draft, setDraft] = useState<string | null>(null)
  const shown = draft ?? (value !== null ? String(value) : '')

  return (
    <span className="shrink-0 text-right">
      <input
        inputMode="text" value={shown}
        placeholder={placeholder}
        onChange={(e) => setDraft(e.target.value)}
        onClick={(e) => e.stopPropagation()}
        onBlur={() => {
          if (draft === null) return
          const reading = readAmount(draft)
          // An unreadable draft is left in place rather than silently discarded, so the
          // typo stays on screen next to the message explaining it.
          if (reading.kind === 'invalid') return
          setDraft(null)
          if (reading.kind === 'empty') {
            if (value !== null) onCommit(null)
            return
          }
          if (reading.value !== value) onCommit(reading.value)
        }}
        className="w-28 rounded border border-line px-2 py-1.5 text-right text-sm"
      />
      {draft !== null && <AmountHint raw={draft} className="mt-1" />}
    </span>
  )
}
