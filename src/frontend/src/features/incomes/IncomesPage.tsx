import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { categoriesApi } from '../../api/categories'
import { incomesApi } from '../../api/incomes'
import { ApiError } from '../../api/client'
import { useMoney } from '../../lib/money'
import SearchableSelect, { type SelectOption } from '../../components/SearchableSelect'

function today() {
  return new Date().toISOString().slice(0, 10)
}

export default function IncomesPage() {
  const queryClient = useQueryClient()
  const money = useMoney()
  const [headId, setHeadId] = useState('')
  const [amount, setAmount] = useState('')
  const [date, setDate] = useState(today())
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [filterHeadId, setFilterHeadId] = useState('')

  const { data: categories } = useQuery({
    queryKey: ['categories', 'Income'],
    queryFn: () => categoriesApi.list('Income'),
  })
  const { data: incomes, isLoading } = useQuery({
    queryKey: ['incomes', filterHeadId],
    queryFn: () => incomesApi.list(filterHeadId ? { headId: filterHeadId } : {}),
  })

  const createIncome = useMutation({
    mutationFn: () =>
      incomesApi.create({
        headId,
        amount: Number(amount),
        incomeDate: date,
        note: note.trim() || undefined,
      }),
    onSuccess: () => {
      setAmount('')
      setNote('')
      setError(null)
      queryClient.invalidateQueries({ queryKey: ['incomes'] })
      queryClient.invalidateQueries({ queryKey: ['summary'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not save income.'),
  })

  const removeIncome = useMutation({
    mutationFn: (id: string) => incomesApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['incomes'] })
      queryClient.invalidateQueries({ queryKey: ['summary'] })
    },
  })

  const headOptions: SelectOption[] =
    categories?.flatMap((category) =>
      category.heads.map((head) => ({
        value: head.id,
        label: head.name,
        group: category.name,
      })),
    ) ?? []

  const hasHeads = headOptions.length > 0

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight text-ink">Income</h1>

      {!hasHeads ? (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          Add an income category with at least one head before logging income. Categories → Income.
        </p>
      ) : (
        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (headId && amount) createIncome.mutate()
          }}
          className="flex flex-col gap-3 rounded-xl border border-line bg-card p-4 shadow-sm"
        >
          <SearchableSelect
            value={headId}
            onChange={setHeadId}
            options={headOptions}
            placeholder="Choose a head…"
          />

          <div className="flex gap-2">
            <input
              inputMode="decimal" value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder="Amount"required
              className="flex-1 rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
            />
            <input
              type="date" value={date}
              onChange={(e) => setDate(e.target.value)}
              required
              className="rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
            />
          </div>

          <input
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder="Note (optional)" className="rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
          />

          {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}

          <button
            type="submit" disabled={createIncome.isPending}
            className="rounded-lg bg-brand-600 px-4 py-3 font-medium text-white shadow-sm transition-colors hover:bg-brand-700 active:bg-brand-800 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-brand-500 dark:text-brand-950 dark:hover:bg-brand-400"
          >
            {createIncome.isPending ? 'Adding…' : 'Add income'}
          </button>
        </form>
      )}

      <div className="flex items-center justify-between gap-2">
        <SearchableSelect
          value={filterHeadId}
          onChange={setFilterHeadId}
          options={headOptions}
          emptyLabel="All heads"
          placeholder="All heads"
          className="max-w-[14rem] flex-1"
        />
        {incomes && (
          <span className="text-sm text-ink-muted">
            {incomes.totalCount} · {money.format(incomes.totalAmount)}
          </span>
        )}
      </div>

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      <ul className="flex flex-col gap-2">
        {incomes?.items.map((income) => (
          <li
            key={income.id}
            className="flex items-center justify-between gap-3 rounded-xl border border-line bg-card p-3 shadow-sm"
          >
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-ink">
                {income.categoryName} · {income.headName}
              </p>
              <p className="truncate text-xs text-ink-muted">
                {income.incomeDate}
                {income.note ? ` · ${income.note}` : ''}
              </p>
            </div>
            <div className="flex shrink-0 items-center gap-3">
              <span className="font-medium text-ink">
                {money.format(income.amount)}
              </span>
              <button
                onClick={() => removeIncome.mutate(income.id)}
                className="text-xs font-medium text-ink-muted transition-colors hover:text-negative-600"
              >
                Delete
              </button>
            </div>
          </li>
        ))}
      </ul>

      {incomes?.items.length === 0 && (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          No income yet.
        </p>
      )}
    </div>
  )
}
