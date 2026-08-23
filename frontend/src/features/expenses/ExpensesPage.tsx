import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { categoriesApi } from '../../api/categories'
import { expensesApi } from '../../api/expenses'
import { ApiError } from '../../api/client'

function today() {
  return new Date().toISOString().slice(0, 10)
}

export default function ExpensesPage() {
  const queryClient = useQueryClient()
  const [headId, setHeadId] = useState('')
  const [amount, setAmount] = useState('')
  const [date, setDate] = useState(today())
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [filterHeadId, setFilterHeadId] = useState('')

  const { data: categories } = useQuery({ queryKey: ['categories'], queryFn: () => categoriesApi.list() })
  const { data: expenses, isLoading } = useQuery({
    queryKey: ['expenses', filterHeadId],
    queryFn: () => expensesApi.list(filterHeadId ? { headId: filterHeadId } : {}),
  })

  const createExpense = useMutation({
    mutationFn: () =>
      expensesApi.create({
        headId,
        amount: Number(amount),
        expenseDate: date,
        note: note.trim() || undefined,
      }),
    onSuccess: () => {
      setAmount('')
      setNote('')
      setError(null)
      queryClient.invalidateQueries({ queryKey: ['expenses'] })
      queryClient.invalidateQueries({ queryKey: ['summary'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not save expense.'),
  })

  const removeExpense = useMutation({
    mutationFn: (id: string) => expensesApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['expenses'] })
      queryClient.invalidateQueries({ queryKey: ['summary'] })
    },
  })

  const hasHeads = categories?.some((c) => c.heads.length > 0)

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">Expenses</h1>

      {!hasHeads ? (
        <p className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-400 dark:border-gray-700">
          Add a category with at least one head before logging expenses.
        </p>
      ) : (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (headId && amount) createExpense.mutate()
          }}
          className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900"
        >
          <select
            value={headId}
            onChange={(e) => setHeadId(e.target.value)}
            required
            className="rounded-lg border border-gray-300 px-3 py-2.5 text-base dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
          >
            <option value="">Choose a head…</option>
            {categories?.map((category) => (
              <optgroup key={category.id} label={category.name}>
                {category.heads.map((head) => (
                  <option key={head.id} value={head.id}>
                    {head.name}
                  </option>
                ))}
              </optgroup>
            ))}
          </select>

          <div className="flex gap-2">
            <input
              inputMode="decimal"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder="Amount"
              required
              className="flex-1 rounded-lg border border-gray-300 px-3 py-2.5 text-base dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
            />
            <input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              required
              className="rounded-lg border border-gray-300 px-3 py-2.5 text-base dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
            />
          </div>

          <input
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder="Note (optional)"
            className="rounded-lg border border-gray-300 px-3 py-2.5 text-base dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
          />

          {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}

          <button
            type="submit"
            disabled={createExpense.isPending}
            className="rounded-lg bg-indigo-600 px-4 py-3 font-medium text-white disabled:opacity-50"
          >
            {createExpense.isPending ? 'Adding…' : 'Add expense'}
          </button>
        </form>
      )}

      <div className="flex items-center justify-between gap-2">
        <select
          value={filterHeadId}
          onChange={(e) => setFilterHeadId(e.target.value)}
          className="rounded-lg border border-gray-300 px-2 py-2 text-sm dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100"
        >
          <option value="">All heads</option>
          {categories?.map((category) => (
            <optgroup key={category.id} label={category.name}>
              {category.heads.map((head) => (
                <option key={head.id} value={head.id}>
                  {head.name}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
        {expenses && (
          <span className="text-sm text-gray-500 dark:text-gray-400">
            {expenses.totalCount} · {expenses.totalAmount.toFixed(2)}
          </span>
        )}
      </div>

      {isLoading && <p className="text-gray-500">Loading…</p>}

      <ul className="flex flex-col gap-2">
        {expenses?.items.map((expense) => (
          <li
            key={expense.id}
            className="flex items-center justify-between gap-3 rounded-lg border border-gray-200 bg-white p-3 dark:border-gray-800 dark:bg-gray-900"
          >
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                {expense.categoryName} · {expense.headName}
              </p>
              <p className="truncate text-xs text-gray-500 dark:text-gray-400">
                {expense.expenseDate}
                {expense.note ? ` · ${expense.note}` : ''}
              </p>
            </div>
            <div className="flex shrink-0 items-center gap-3">
              <span className="font-medium text-gray-900 dark:text-gray-100">
                {expense.amount.toFixed(2)}
              </span>
              <button
                onClick={() => removeExpense.mutate(expense.id)}
                className="text-xs text-gray-400 hover:text-red-600"
              >
                Delete
              </button>
            </div>
          </li>
        ))}
      </ul>

      {expenses?.items.length === 0 && (
        <p className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-400 dark:border-gray-700">
          No expenses yet.
        </p>
      )}
    </div>
  )
}
