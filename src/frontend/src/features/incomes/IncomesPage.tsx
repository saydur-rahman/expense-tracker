import { useState } from 'react'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { categoriesApi } from '../../api/categories'
import { incomesApi } from '../../api/incomes'
import { ApiError } from '../../api/client'
import { useMoney } from '../../lib/money'
import { amountValue } from '../../lib/calc'
import PeriodPicker from '../../components/PeriodPicker'
import { budgetPeriodsApi } from '../../api/settings'
import AmountField from '../../components/AmountField'
import SearchableSelect, { type SelectOption } from '../../components/SearchableSelect'

function today() {
  return new Date().toISOString().slice(0, 10)
}

const PAGE_SIZE = 25

export default function IncomesPage() {
  const queryClient = useQueryClient()
  const money = useMoney()
  const [headId, setHeadId] = useState('')
  const [amount, setAmount] = useState('')
  const [date, setDate] = useState(today())
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [filterHeadId, setFilterHeadId] = useState('')
  const [offset, setOffset] = useState(0)

  // The list follows the chosen cycle, so what is on screen always matches the period
  // the dashboard and budgets are showing.
  const { data: period } = useQuery({
    queryKey: ['budget-period', offset],
    queryFn: () => budgetPeriodsApi.relative(offset),
  })

  const { data: categories } = useQuery({
    queryKey: ['categories', 'Income'],
    queryFn: () => categoriesApi.list('Income'),
  })
  // Paged rather than one big request: the API caps a page at 100, so asking for
  // "everything so far" would quietly stop loading once a period passed that.
  const {
    data: incomesPages,
    isLoading,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useInfiniteQuery({
    queryKey: ['incomes', period?.id, filterHeadId],
    enabled: !!period,
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      incomesApi.list({
        from: period!.startDate,
        to: period!.endDate,
        page: pageParam,
        pageSize: PAGE_SIZE,
        ...(filterHeadId ? { headId: filterHeadId } : {}),
      }),
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
  })

  const items = incomesPages?.pages.flatMap((p) => p.items) ?? []
  // Totals describe the whole period, not the rows loaded so far, so they come off any page.
  const totalCount = incomesPages?.pages[0]?.totalCount ?? 0
  const totalAmount = incomesPages?.pages[0]?.totalAmount ?? 0

  const createIncome = useMutation({
    mutationFn: () =>
      incomesApi.create({
        headId,
        amount: amountValue(amount) ?? 0,
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
            if (headId && amountValue(amount) !== null) createIncome.mutate()
          }}
          className="flex flex-col gap-3 rounded-xl border border-line bg-card p-4 shadow-sm"
        >
          <SearchableSelect
            value={headId}
            onChange={setHeadId}
            options={headOptions}
            placeholder="Choose a head…"
          />

          {/* items-start so the date keeps its height when the amount grows a hint. */}
          <div className="flex items-start gap-2">
            <AmountField
              value={amount}
              onChange={setAmount}
              placeholder="Amount" required
              wrapperClassName="flex-1"
              className="w-full rounded-lg border border-line bg-card px-3 py-2.5 text-base transition-colors focus:border-brand-500 focus:outline-none"
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

      <PeriodPicker label={period?.label ?? '…'} offset={offset} onOffsetChange={setOffset} />

      <div className="flex items-center justify-between gap-2">
        <SearchableSelect
          value={filterHeadId}
          onChange={setFilterHeadId}
          options={headOptions}
          emptyLabel="All heads"
          placeholder="All heads"
          className="max-w-[14rem] flex-1"
        />
        {!isLoading && (
          <span className="text-sm text-ink-muted">
            {totalCount} · {money.format(totalAmount)}
          </span>
        )}
      </div>

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      {!isLoading && totalCount === 0 && (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          Nothing logged in this period.
        </p>
      )}

      <ul className="flex flex-col gap-2">
        {items.map((income) => (
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

      {hasNextPage && (
        <button
          type="button"
          onClick={() => fetchNextPage()}
          disabled={isFetchingNextPage}
          className="self-center rounded-lg border border-line bg-card px-4 py-2 text-sm font-medium text-ink-soft shadow-sm transition-colors hover:bg-raised disabled:opacity-50"
        >
          {isFetchingNextPage ? 'Loading…' : `Load more · ${items.length} of ${totalCount}`}
        </button>
      )}

    </div>
  )
}
