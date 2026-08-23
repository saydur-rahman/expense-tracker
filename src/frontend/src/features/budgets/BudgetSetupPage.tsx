import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { budgetPeriodsApi } from '../../api/settings'
import { budgetsApi, type CategoryBudget } from '../../api/budgets'
import { ApiError } from '../../api/client'
import PeriodPicker from '../../components/PeriodPicker'

export default function BudgetSetupPage() {
  const [offset, setOffset] = useState(0)

  const { data: period } = useQuery({
    queryKey: ['budget-period', offset],
    queryFn: () => budgetPeriodsApi.relative(offset),
  })

  const { data: budgets, isLoading } = useQuery({
    queryKey: ['budgets', period?.id],
    queryFn: () => budgetsApi.get(period!.id),
    enabled: !!period,
  })

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">Budgets</h1>
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Set a budget per category, then split it across its heads.
        </p>
      </div>

      <PeriodPicker label={period?.label ?? '…'} offset={offset} onOffsetChange={setOffset} />

      {isLoading && <p className="text-gray-500">Loading…</p>}

      {budgets?.categories.length === 0 && (
        <p className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-400 dark:border-gray-700">
          Add a category first, then you can budget for it here.
        </p>
      )}

      <div className="flex flex-col gap-3">
        {budgets?.categories.map((category) => (
          <CategoryBudgetCard key={category.categoryId} periodId={budgets.periodId} category={category} />
        ))}
      </div>
    </div>
  )
}

function CategoryBudgetCard({ periodId, category }: { periodId: string; category: CategoryBudget }) {
  const queryClient = useQueryClient()
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
    <div className="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex items-center justify-between gap-3">
        <span className="font-medium text-gray-900 dark:text-gray-100">{category.categoryName}</span>
        <AmountInput
          value={category.amount}
          placeholder="Set budget"
          onCommit={(amount) =>
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
            <div className="h-2 overflow-hidden rounded-full bg-gray-200 dark:bg-gray-800">
              <div
                className={`h-full transition-all ${overAllocated ? 'bg-red-500' : 'bg-indigo-500'}`}
                style={{
                  width: `${Math.min(100, category.amount > 0 ? (category.allocatedToHeads / category.amount) * 100 : 0)}%`,
                }}
              />
            </div>
            <p className={`mt-1 text-xs ${overAllocated ? 'text-red-600 dark:text-red-400' : 'text-gray-500 dark:text-gray-400'}`}>
              {category.allocatedToHeads.toFixed(2)} of {category.amount.toFixed(2)} allocated ·{' '}
              {(category.unallocated ?? 0).toFixed(2)} left for heads
            </p>
          </div>

          <ul className="mt-3 flex flex-col gap-2 border-t border-gray-100 pt-3 dark:border-gray-800">
            {category.heads.map((head) => (
              <li key={head.headId} className="flex items-center justify-between gap-3">
                <span className="text-sm text-gray-700 dark:text-gray-300">{head.headName}</span>
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
            {category.heads.length === 0 && (
              <li className="text-xs text-gray-400">No heads in this category yet.</li>
            )}
          </ul>
        </>
      )}

      {error && <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>}
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
      inputMode="decimal"
      value={shown}
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
      className="w-28 rounded border border-gray-300 px-2 py-1.5 text-right text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
    />
  )
}
