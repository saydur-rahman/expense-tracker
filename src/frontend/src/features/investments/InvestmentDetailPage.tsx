import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  investmentsApi,
  type Investment,
  type InvestmentTransaction,
} from '../../api/investments'
import { useMoney } from '../../lib/money'
import Button from '../../components/Button'
import DateRangePicker from '../../components/DateRangePicker'
import TwoSliceDonut from '../../components/charts/TwoSliceDonut'
import LegendRow from '../../components/charts/LegendRow'
import PeriodBars from '../../components/charts/PeriodBars'
import { LEFT_COLOR, OVER_COLOR, SPENT_COLOR } from '../../components/charts/colors'
import { card, emptyState, eyebrow, pageTitle } from '../../components/ui'
import { InvestmentForm } from './InvestmentsPage'
import { wordingFor } from './wording'

const PAGE_SIZE = 20

export default function InvestmentDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { format } = useMoney()

  const [editing, setEditing] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [range, setRange] = useState({ from: '', to: '' })

  const { data: detail, isLoading } = useQuery({
    queryKey: ['investment', id],
    queryFn: () => investmentsApi.get(id),
  })

  const { data: byPeriod } = useQuery({
    queryKey: ['investment-by-period', id],
    queryFn: () => investmentsApi.byPeriod(id),
  })

  const filtered = range.from !== '' || range.to !== ''

  const transactions = useInfiniteQuery({
    queryKey: ['investment-transactions', id, range.from, range.to],
    enabled: filtered,
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      investmentsApi.transactions(id, {
        ...(range.from ? { from: range.from } : {}),
        ...(range.to ? { to: range.to } : {}),
        page: pageParam,
        pageSize: PAGE_SIZE,
      }),
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
  })

  const remove = useMutation({
    mutationFn: () => investmentsApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['investments'] })
      navigate('/investments')
    },
  })

  if (isLoading) return <p className="text-ink-muted">Loading…</p>
  if (!detail) return <p className={emptyState}>That investment is no longer here.</p>

  const investment = detail.investment
  const words = wordingFor(investment.kind)
  const shown: InvestmentTransaction[] = filtered
    ? (transactions.data?.pages.flatMap((p) => p.items) ?? [])
    : detail.recentTransactions
  const firstPage = transactions.data?.pages[0]
  const shownCount = filtered ? (firstPage?.totalCount ?? 0) : detail.transactionCount

  return (
    <div className="flex flex-col gap-4">
      <div>
        <Link to="/investments" className="text-xs text-ink-muted hover:text-ink">
          ‹ Investments &amp; lending
        </Link>
        <h1 className={pageTitle}>{investment.name}</h1>
        {investment.counterparty && (
          <p className="text-sm text-ink-muted">to {investment.counterparty}</p>
        )}
      </div>

      {editing ? (
        <InvestmentForm investment={investment} onDone={() => setEditing(false)} />
      ) : (
        <>
          <InvestmentSummary investment={investment} />

          {investment.remark && (
            <p className={`${card} p-4 text-sm text-ink-soft`}>
              <span className={`${eyebrow} block`}>Why</span>
              {investment.remark}
            </p>
          )}

          <section className={`${card} p-4`}>
            <h2 className={`${eyebrow} mb-2`}>{words.outEachCycle}</h2>
            <PeriodBars
              buckets={(byPeriod ?? []).map((p) => ({ label: p.label, amount: p.amount }))}
              color={SPENT_COLOR}
              emptyLabel={`Nothing ${words.outEntry} during the cycles shown.`}
            />

            <h2 className={`${eyebrow} mb-2 mt-4`}>{words.backEachCycle}</h2>
            <PeriodBars
              buckets={(byPeriod ?? []).map((p) => ({
                label: p.label,
                amount: p.secondaryAmount,
              }))}
              color={LEFT_COLOR}
              emptyLabel="Nothing back yet in the cycles shown."
            />
          </section>

          <section className={`${card} p-4`}>
            <div className="mb-2 flex flex-wrap items-baseline justify-between gap-2">
              <h2 className={eyebrow}>{filtered ? 'In range' : 'Latest movements'}</h2>
              <span className="text-xs text-ink-muted">
                {shownCount} {shownCount === 1 ? 'entry' : 'entries'}
                {firstPage && filtered && (
                  <>
                    {' · '}
                    {format(firstPage.totalInvested)} in, {format(firstPage.totalReturned)} back
                  </>
                )}
              </span>
            </div>

            <DateRangePicker from={range.from} to={range.to} onChange={setRange} />

            {shown.length === 0 ? (
              <p className="mt-3 text-sm text-ink-muted">
                {filtered
                  ? 'Nothing in that range.'
                  : 'Nothing yet. Log an expense on a linked spending head and it will appear here.'}
              </p>
            ) : (
              <ul className="mt-3 flex flex-col gap-2">
                {shown.map((t) => {
                  const isReturn = t.direction === 'Return'
                  return (
                    <li
                      key={`${t.direction}-${t.id}`}
                      className="flex items-baseline justify-between gap-2"
                    >
                      <span className="min-w-0">
                        <span className="block truncate text-sm text-ink">
                          {t.categoryName} › {t.headName}
                        </span>
                        <span className="block text-xs text-ink-muted">
                          {t.date} · {isReturn ? words.backEntry : words.outEntry}
                          {t.note && ` · ${t.note}`}
                        </span>
                      </span>
                      <span
                        className={`shrink-0 text-sm font-medium tabular-nums ${
                          isReturn ? 'text-positive-700 dark:text-positive-400' : 'text-ink'
                        }`}
                      >
                        {isReturn ? '+' : '−'}
                        {format(t.amount)}
                      </span>
                    </li>
                  )
                })}
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
              Removing it leaves every expense and income exactly where it is — you only lose
              this view of them.
            </p>
          )}
        </>
      )}
    </div>
  )
}

function InvestmentSummary({ investment }: { investment: Investment }) {
  const { format } = useMoney()
  const words = wordingFor(investment.kind)

  return (
    <section className={`${card} p-4`}>
      <div className="flex items-center gap-4">
        {/* Same reading as a loan: red is capital still out there, green is what has
            found its way back. */}
        <TwoSliceDonut
          size={132}
          first={{ label: words.back, value: investment.returned, color: LEFT_COLOR }}
          second={{
            label: words.remaining,
            value: investment.outstanding,
            color: investment.isRecouped ? LEFT_COLOR : OVER_COLOR,
          }}
          centre={
            <span>
              <span className={`${eyebrow} block`}>
                {investment.isRecouped ? words.surplus : words.remaining}
              </span>
              <span
                className={`block text-sm font-semibold tabular-nums ${
                  investment.isRecouped
                    ? 'text-positive-700 dark:text-positive-400'
                    : 'text-negative-600 dark:text-negative-400'
                }`}
              >
                {format(investment.isRecouped ? investment.gain : investment.outstanding)}
              </span>
            </span>
          }
        />

        <dl className="flex min-w-0 flex-1 flex-col gap-2">
          <LegendRow label={words.out} value={format(investment.invested)} />
          <LegendRow swatch={LEFT_COLOR} label={words.back} value={format(investment.returned)} />
          <LegendRow
            swatch={investment.isRecouped ? LEFT_COLOR : OVER_COLOR}
            label={investment.isRecouped ? words.surplus : words.remaining}
            value={format(investment.isRecouped ? investment.gain : investment.outstanding)}
            emphasis
          />
        </dl>
      </div>

      <p className="mt-3 border-t border-line-soft pt-3 text-xs text-ink-muted">
        {investment.invested === 0
          ? words.nothingYet(investment.startedOn)
          : investment.isRecouped
            ? words.recouped(format(investment.gain))
            : words.progress(investment.percentReturned, investment.startedOn)}
      </p>
    </section>
  )
}
