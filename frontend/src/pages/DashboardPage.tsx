import { useQuery } from '@tanstack/react-query'
import { reportsApi, type CategorySummary } from '../api/reports'

export default function DashboardPage() {
  const { data: summary, isLoading } = useQuery({
    queryKey: ['summary', 'current'],
    queryFn: reportsApi.currentSummary,
  })

  if (isLoading) return <p className="text-gray-500">Loading…</p>

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">Dashboard</h1>
        <p className="text-sm text-gray-500 dark:text-gray-400">{summary?.periodLabel}</p>
      </div>

      <div className="grid grid-cols-3 gap-2">
        <Stat label="Budget" value={summary?.totalBudget ?? 0} />
        <Stat label="Spent" value={summary?.totalSpent ?? 0} />
        <Stat
          label="Left"
          value={summary?.totalRemaining ?? 0}
          danger={(summary?.totalRemaining ?? 0) < 0}
        />
      </div>

      {summary?.categories.length === 0 && (
        <p className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-400 dark:border-gray-700">
          Nothing to show yet. Add a category, set a budget, then log an expense.
        </p>
      )}

      <div className="flex flex-col gap-3">
        {summary?.categories.map((category) => (
          <CategoryCard key={category.categoryId} category={category} />
        ))}
      </div>
    </div>
  )
}

function Stat({ label, value, danger }: { label: string; value: number; danger?: boolean }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-3 text-center dark:border-gray-800 dark:bg-gray-900">
      <p className="text-xs text-gray-500 dark:text-gray-400">{label}</p>
      <p
        className={`text-lg font-semibold ${
          danger ? 'text-red-600 dark:text-red-400' : 'text-gray-900 dark:text-gray-100'
        }`}
      >
        {value.toFixed(2)}
      </p>
    </div>
  )
}

function CategoryCard({ category }: { category: CategorySummary }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex items-baseline justify-between gap-2">
        <span className="font-medium text-gray-900 dark:text-gray-100">
          {category.categoryName}
          {category.isArchived && (
            <span className="ml-2 text-xs font-normal text-gray-400">removed</span>
          )}
        </span>
        <span className="text-sm text-gray-500 dark:text-gray-400">
          {category.spent.toFixed(2)}
          {category.budget !== null && ` / ${category.budget.toFixed(2)}`}
        </span>
      </div>

      <ProgressBar spent={category.spent} budget={category.budget} />

      <ul className="mt-3 flex flex-col gap-2 border-t border-gray-100 pt-3 dark:border-gray-800">
        {category.heads.map((head) => (
          <li key={head.headId}>
            <div className="flex items-baseline justify-between gap-2">
              <span className="text-sm text-gray-700 dark:text-gray-300">
                {head.headName}
                {head.isArchived && <span className="ml-2 text-xs text-gray-400">removed</span>}
              </span>
              <span
                className={`text-sm ${
                  head.isOverBudget ? 'text-red-600 dark:text-red-400' : 'text-gray-500 dark:text-gray-400'
                }`}
              >
                {head.spent.toFixed(2)}
                {head.budget !== null && ` / ${head.budget.toFixed(2)}`}
              </span>
            </div>
            <ProgressBar spent={head.spent} budget={head.budget} thin />
          </li>
        ))}
      </ul>
    </div>
  )
}

function ProgressBar({
  spent,
  budget,
  thin,
}: {
  spent: number
  budget: number | null
  thin?: boolean
}) {
  if (budget === null || budget === 0) {
    return null
  }

  const pct = Math.min(100, (spent / budget) * 100)
  const over = spent > budget

  return (
    <div className={`mt-1.5 overflow-hidden rounded-full bg-gray-200 dark:bg-gray-800 ${thin ? 'h-1' : 'h-2'}`}>
      <div
        className={`h-full transition-all ${over ? 'bg-red-500' : 'bg-indigo-500'}`}
        style={{ width: `${pct}%` }}
      />
    </div>
  )
}
