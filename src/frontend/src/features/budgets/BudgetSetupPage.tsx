import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { budgetPeriodsApi } from '../../api/settings'
import { budgetsApi, type CategoryBudget } from '../../api/budgets'
import { ApiError } from '../../api/client'
import PeriodPicker from '../../components/PeriodPicker'
import { useMoney } from '../../lib/money'

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
          Set a budget per category, then split it across its heads.
        </p>
      </div>

      <PeriodPicker label={period?.label ?? '…'} offset={offset} onOffsetChange={setOffset} />

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

      <div className="flex flex-col gap-3">
        {visibleCategories.map((category) => (
          <CategoryBudgetCard key={category.categoryId} periodId={budgets!.periodId} category={category} />
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

function CategoryBudgetCard({ periodId, category }: { periodId: string; category: CategoryBudget }) {
  const queryClient = useQueryClient()
  const money = useMoney()
  const [error, setError] = useState<string | null>(null)

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['budgets'] })

  const mutation = useMutation({
    mutationFn: (fn: () => Promise<unknown>) => fn(),
    onSuccess: () => {
      setError(null)
      refresh()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not save.'),
  })

  const overAllocated = category.unallocated !== null && category.unallocated < 0

  return (
    <div className="rounded-xl border border-line bg-card p-4 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <span className="font-medium text-ink">{category.categoryName}</span>
        <AmountInput
          value={category.amount}
          placeholder="Set budget" onCommit={(amount) =>
            mutation.mutate(() =>
              amount === null
                ? budgetsApi.clearCategory(periodId, category.categoryId)
                : budgetsApi.setCategory(periodId, category.categoryId, amount),
            )
          }
        />
      </div>

      {category.amount !== null && (
        <>
          <div className="mt-3">
            <div className="h-2 overflow-hidden rounded-full bg-track">
              <div
                className={`h-full transition-all ${overAllocated ? 'bg-negative-500' : 'bg-brand-500'}`}
                style={{
                  width: `${Math.min(100, category.amount > 0 ? (category.allocatedToHeads / category.amount) * 100 : 0)}%`,
                }}
              />
            </div>
            <p className={`mt-1 text-xs ${overAllocated ? 'text-negative-600 dark:text-negative-400' : 'text-ink-muted'}`}>
              {money.format(category.allocatedToHeads)} of {money.format(category.amount)} allocated ·{' '}
              {money.format(category.unallocated ?? 0)} left for heads
            </p>
          </div>

          <ul className="mt-3 flex flex-col gap-2 border-t border-line-soft pt-3">
            {category.heads.map((head) => (
              <li key={head.headId} className="flex items-center justify-between gap-3">
                <span className="text-sm text-ink-soft">{head.headName}</span>
                <AmountInput
                  value={head.amount}
                  placeholder="—" onCommit={(amount) =>
                    mutation.mutate(() =>
                      amount === null
                        ? budgetsApi.clearHead(periodId, head.headId)
                        : budgetsApi.setHead(periodId, head.headId, amount),
                    )
                  }
                />
              </li>
            ))}
            {category.heads.length === 0 && (
              <li className="text-xs text-ink-muted">No heads in this category yet.</li>
            )}
          </ul>
        </>
      )}

      {error && <p className="mt-2 text-sm text-negative-600 dark:text-negative-400">{error}</p>}
    </div>
  )
}

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
    <input
      inputMode="decimal" value={shown}
      placeholder={placeholder}
      onChange={(e) => setDraft(e.target.value)}
      onBlur={() => {
        if (draft === null) return
        const trimmed = draft.trim()
        setDraft(null)
        if (trimmed === '') {
          if (value !== null) onCommit(null)
          return
        }
        const parsed = Number(trimmed)
        if (!Number.isNaN(parsed) && parsed !== value) onCommit(parsed)
      }}
      className="w-28 rounded border border-line px-2 py-1.5 text-right text-sm"
    />
  )
}
