import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { loansApi, type Loan, type LoanTransaction } from '../../api/loans'
import { useMoney } from '../../lib/money'
import Button from '../../components/Button'
import DateRangePicker from '../../components/DateRangePicker'
import TwoSliceDonut from '../../components/charts/TwoSliceDonut'
import LegendRow from '../../components/charts/LegendRow'
import PeriodBars from '../../components/charts/PeriodBars'
import { LEFT_COLOR, OVER_COLOR, SPENT_COLOR } from '../../components/charts/colors'
import { card, emptyState, eyebrow, pageTitle } from '../../components/ui'
import { LoanForm } from './LoansPage'

const PAGE_SIZE = 20

export default function LoanDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { format } = useMoney()

  const [editing, setEditing] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [range, setRange] = useState({ from: '', to: '' })

  const { data: detail, isLoading } = useQuery({
    queryKey: ['loan', id],
    queryFn: () => loansApi.get(id),
  })

  const { data: byPeriod } = useQuery({
    queryKey: ['loan-by-period', id],
    queryFn: () => loansApi.byPeriod(id),
  })

  // Only asked for once a range is set — until then the detail call has already
  // returned the most recent 20, so this would be the same rows twice.
  const filtered = range.from !== '' || range.to !== ''

  const transactions = useInfiniteQuery({
    queryKey: ['loan-transactions', id, range.from, range.to],
    enabled: filtered,
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      loansApi.transactions(id, {
        ...(range.from ? { from: range.from } : {}),
        ...(range.to ? { to: range.to } : {}),
        page: pageParam,
        pageSize: PAGE_SIZE,
      }),
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
  })

  const remove = useMutation({
    mutationFn: () => loansApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['loans'] })
      navigate('/loans')
    },
  })

  if (isLoading) return <p className="text-ink-muted">Loading…</p>
  if (!detail) return <p className={emptyState}>That loan is no longer here.</p>

  const loan = detail.loan
  const shown: LoanTransaction[] = filtered
    ? (transactions.data?.pages.flatMap((p) => p.items) ?? [])
    : detail.recentTransactions
  const shownTotal = filtered
    ? transactions.data?.pages[0]?.totalAmount
    : undefined
  const shownCount = filtered
    ? (transactions.data?.pages[0]?.totalCount ?? 0)
    : detail.transactionCount

  return (
    <div className="flex flex-col gap-4">
      <div>
        <Link to="/loans" className="text-xs text-ink-muted hover:text-ink">
          ‹ Loans
        </Link>
        <h1 className={pageTitle}>{loan.name}</h1>
        {loan.lender && <p className="text-sm text-ink-muted">from {loan.lender}</p>}
      </div>

      {editing ? (
        <LoanForm loan={loan} onDone={() => setEditing(false)} />
      ) : (
        <>
          <LoanSummary loan={loan} />

          {loan.remark && (
            <p className={`${card} p-4 text-sm text-ink-soft`}>
              <span className={`${eyebrow} block`}>Why</span>
              {loan.remark}
            </p>
          )}

          <section className={`${card} p-4`}>
            <h2 className={`${eyebrow} mb-2`}>Paid each cycle</h2>
            <PeriodBars
              buckets={(byPeriod ?? []).map((p) => ({ label: p.label, amount: p.amount }))}
              color={SPENT_COLOR}
              emptyLabel="No payments in the cycles shown."
            />
          </section>

          <section className={`${card} p-4`}>
            <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
              <h2 className={eyebrow}>
                {filtered ? 'Payments in range' : 'Latest payments'}
              </h2>
              <span className="text-xs text-ink-muted">
                {shownCount} {shownCount === 1 ? 'payment' : 'payments'}
                {shownTotal !== undefined && ` · ${format(shownTotal)}`}
              </span>
            </div>

            <DateRangePicker from={range.from} to={range.to} onChange={setRange} />

            {shown.length === 0 ? (
              <p className="mt-3 text-sm text-ink-muted">
                {filtered
                  ? 'Nothing in that range.'
                  : loan.heads.length === 0
                    ? 'Link a head and your expenses on it will appear here.'
                    : 'No payments yet. Log an expense on a linked head and it will show up here.'}
              </p>
            ) : (
              <ul className="mt-3 flex flex-col gap-2">
                {shown.map((t) => (
                  <li key={t.id} className="flex items-baseline justify-between gap-2">
                    <span className="min-w-0">
                      <span className="block truncate text-sm text-ink">
                        {t.categoryName} › {t.headName}
                      </span>
                      <span className="block text-xs text-ink-muted">
                        {t.date}
                        {t.note && ` · ${t.note}`}
                      </span>
                    </span>
                    <span className="shrink-0 text-sm font-medium tabular-nums text-ink">
                      {format(t.amount)}
                    </span>
                  </li>
                ))}
              </ul>
            )}

            {!filtered && detail.transactionCount > shown.length && (
              <p className="mt-3 text-xs text-ink-muted">
                Showing the latest {shown.length} of {detail.transactionCount}. Set a date
                range to see the rest.
              </p>
            )}

            {filtered && transactions.hasNextPage && (
              <Button
                variant="secondary"
                size="sm"
                block
                className="mt-3"
                onClick={() => transactions.fetchNextPage()}
                disabled={transactions.isFetchingNextPage}
              >
                {transactions.isFetchingNextPage
                  ? 'Loading…'
                  : `Load more · ${shown.length} of ${shownCount}`}
              </Button>
            )}
          </section>

          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" onClick={() => setEditing(true)}>
              Edit
            </Button>
            {confirmingDelete ? (
              <>
                <Button variant="danger" onClick={() => remove.mutate()} disabled={remove.isPending}>
                  {remove.isPending ? 'Removing…' : 'Really remove'}
                </Button>
                <Button variant="ghost" onClick={() => setConfirmingDelete(false)}>
                  Keep it
                </Button>
              </>
            ) : (
              <Button variant="ghost" onClick={() => setConfirmingDelete(true)}>
                Remove
              </Button>
            )}
          </div>

          {confirmingDelete && (
            <p className="text-xs text-ink-muted">
              Removing the loan leaves every expense exactly where it is — you only lose
              this view of them.
            </p>
          )}
        </>
      )}
    </div>
  )
}

function LoanSummary({ loan }: { loan: Loan }) {
  const { format } = useMoney()

  return (
    <section className={`${card} p-4`}>
      <div className="flex items-center gap-4">
        {/* Wholly red until the first payment, filling green as it clears — the ring
            measures progress, so green is the part that is done. */}
        <TwoSliceDonut
          size={132}
          first={{ label: 'Repaid', value: loan.repaid, color: LEFT_COLOR }}
          second={{
            label: 'Outstanding',
            value: loan.outstanding,
            color: loan.isSettled ? LEFT_COLOR : OVER_COLOR,
          }}
          centre={
            <span>
              <span className={`${eyebrow} block`}>{loan.isSettled ? 'Cleared' : 'Left'}</span>
              <span
                className={`block text-sm font-semibold tabular-nums ${
                  loan.isSettled
                    ? 'text-positive-700 dark:text-positive-400'
                    : 'text-negative-600 dark:text-negative-400'
                }`}
              >
                {format(loan.outstanding)}
              </span>
            </span>
          }
        />

        <dl className="flex min-w-0 flex-1 flex-col gap-2">
          <LegendRow label="Borrowed" value={format(loan.amountTaken)} />
          <LegendRow swatch={LEFT_COLOR} label="Repaid" value={format(loan.repaid)} />
          <LegendRow
            swatch={loan.isSettled ? LEFT_COLOR : OVER_COLOR}
            label="Outstanding"
            value={format(loan.outstanding)}
            emphasis
          />
        </dl>
      </div>

      <p className="mt-3 border-t border-line-soft pt-3 text-xs text-ink-muted">
        {loan.isSettled
          ? 'Fully repaid.'
          : `${loan.percentSettled}% paid off · taken ${loan.takenOn}`}
      </p>

      {loan.overpaid > 0 && (
        <p className="mt-1 text-xs text-negative-600 dark:text-negative-400">
          You have paid {format(loan.overpaid)} more than you borrowed. Usually that means a
          payment went onto a linked head that wasn't really for this loan.
        </p>
      )}

      {loan.heads.length > 0 && (
        <p className="mt-2 text-xs text-ink-muted">
          Counting spending on{' '}
          {loan.heads.map((h) => `${h.categoryName} › ${h.headName}`).join(', ')}
          {loan.heads.some((h) => h.isArchived) && ' (one has since been removed)'}
        </p>
      )}
    </section>
  )
}
