import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { loansApi, type Loan, type LoanPortfolio } from '../../api/loans'
import { categoriesApi } from '../../api/categories'
import { budgetPeriodsApi } from '../../api/settings'
import { ApiError } from '../../api/client'
import { useMoney } from '../../lib/money'
import Button from '../../components/Button'
import AmountField from '../../components/AmountField'
import HeadMultiSelect from '../../components/HeadMultiSelect'
import LinkedHeadWarning from '../../components/LinkedHeadWarning'
import ProgressBar from '../../components/charts/ProgressBar'
import PeriodPicker from '../../components/PeriodPicker'
import { amountValue } from '../../lib/calc'
import { card, emptyState, field, eyebrow, pageTitle } from '../../components/ui'
import { todayLocal } from '../../lib/dates'

export default function LoansPage() {
  const [adding, setAdding] = useState(false)
  const [offset, setOffset] = useState(0)

  const { data: loans, isLoading } = useQuery({ queryKey: ['loans'], queryFn: loansApi.list })

  const { data: period } = useQuery({
    queryKey: ['budget-period', offset],
    queryFn: () => budgetPeriodsApi.relative(offset),
  })

  const { data: portfolio } = useQuery({
    queryKey: ['loans', 'portfolio', period?.id],
    queryFn: () => loansApi.portfolio(period!.id),
    enabled: !!period,
  })

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className={pageTitle}>Loans</h1>
        <p className="text-sm text-ink-muted">
          What you borrowed, and how much of it you have paid back.
        </p>
      </div>

      <PeriodPicker label={period?.label ?? '…'} offset={offset} onOffsetChange={setOffset} />

      {portfolio && portfolio.count > 0 && <LoanTotals portfolio={portfolio} />}

      {adding ? (
        <LoanForm onDone={() => setAdding(false)} />
      ) : (
        <Button onClick={() => setAdding(true)}>Add a loan</Button>
      )}

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      {loans?.length === 0 && (
        <p className={emptyState}>
          No loans yet. Add one, link the head you repay it through, and every expense you
          log there will count against it.
        </p>
      )}

      <div className="flex flex-col gap-2">
        {loans?.map((loan) => <LoanCard key={loan.id} loan={loan} />)}
      </div>
    </div>
  )
}

/**
 * Everything owed, in one place. The period picker above changes only the last line —
 * the balance is the balance whichever cycle you happen to be looking at.
 */
function LoanTotals({ portfolio }: { portfolio: LoanPortfolio }) {
  const { format } = useMoney()

  return (
    <section className={`${card} p-4`}>
      <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2">
        <span>
          <span className={`${eyebrow} block`}>Still owed</span>
          <span className="block text-xl font-semibold tabular-nums text-negative-600 dark:text-negative-400">
            {format(portfolio.outstanding)}
          </span>
        </span>
        <span className="text-right">
          <span className={`${eyebrow} block`}>Borrowed</span>
          <span className="block text-lg font-semibold tabular-nums text-ink">
            {format(portfolio.borrowed)}
          </span>
        </span>
        <span className="text-right">
          <span className={`${eyebrow} block`}>Repaid</span>
          <span className="block text-lg font-semibold tabular-nums text-positive-700 dark:text-positive-400">
            {format(portfolio.repaid)}
          </span>
        </span>
      </div>

      <ProgressBar
        value={portfolio.repaid}
        total={portfolio.borrowed}
        fill="bg-positive-600"
        overFill="bg-positive-600"
      />

      <p className="mt-2 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-t border-line-soft pt-2 text-xs text-ink-muted">
        <span>
          {portfolio.percentSettled}% paid off
          {portfolio.settledCount > 0 &&
            ` · ${portfolio.settledCount} of ${portfolio.count} settled`}
        </span>
        <span>
          Paid in {portfolio.periodLabel}{' '}
          <strong className="font-semibold tabular-nums text-ink">
            {format(portfolio.paidInPeriod)}
          </strong>
        </span>
      </p>
    </section>
  )
}

function LoanCard({ loan }: { loan: Loan }) {
  const { format } = useMoney()

  return (
    <Link
      to={`/loans/${loan.id}`}
      className={`${card} block px-4 py-3 transition-colors hover:bg-raised`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="min-w-0">
          <span className="block truncate font-medium text-ink">{loan.name}</span>
          {loan.lender && (
            <span className="block truncate text-xs text-ink-muted">{loan.lender}</span>
          )}
        </span>
        <span className="shrink-0 text-right">
          <span
            className={`block text-sm font-semibold tabular-nums ${
              loan.isSettled
                ? 'text-positive-700 dark:text-positive-400'
                : 'text-negative-600 dark:text-negative-400'
            }`}
          >
            {loan.isSettled ? 'Settled' : format(loan.outstanding)}
          </span>
          <span className="block text-xs text-ink-muted">
            {format(loan.repaid)} of {format(loan.amountTaken)}
          </span>
        </span>
      </div>

      {/* Green as it clears, because the bar measures what is *done*, not what is left. */}
      <ProgressBar
        value={loan.repaid}
        total={loan.amountTaken}
        fill="bg-positive-600"
        overFill="bg-positive-600"
      />

      {loan.heads.length === 0 && (
        <p className="mt-1.5 text-xs text-negative-600 dark:text-negative-400">
          No head linked yet — nothing will count against this loan until you link one.
        </p>
      )}
    </Link>
  )
}

export function LoanForm({
  loan,
  onDone,
}: {
  loan?: Loan
  onDone: () => void
}) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(loan?.name ?? '')
  const [lender, setLender] = useState(loan?.lender ?? '')
  const [amount, setAmount] = useState(loan ? String(loan.amountTaken) : '')
  const [takenOn, setTakenOn] = useState(loan?.takenOn ?? todayLocal())
  const [remark, setRemark] = useState(loan?.remark ?? '')
  const [headIds, setHeadIds] = useState<string[]>(loan?.heads.map((h) => h.headId) ?? [])
  const [error, setError] = useState<string | null>(null)

  // Spending heads only: a loan is repaid out of what goes out.
  const { data: categories } = useQuery({
    queryKey: ['categories', 'Expense'],
    queryFn: () => categoriesApi.list('Expense'),
  })

  const mutation = useMutation({
    mutationFn: () => {
      const request = {
        name: name.trim(),
        lender: lender.trim() || null,
        amountTaken: amountValue(amount) ?? 0,
        takenOn,
        remark: remark.trim() || null,
        headIds,
      }
      return loan ? loansApi.update(loan.id, request) : loansApi.create(request)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['loans'] })
      onDone()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not save.'),
  })

  return (
    <form
      className={`${card} flex flex-col gap-3 p-4`}
      onSubmit={(e) => {
        e.preventDefault()
        setError(null)
        mutation.mutate()
      }}
    >
      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">What is it?</span>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Car loan"
          className={field}
        />
      </label>

      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">Who is it from? (optional)</span>
        <input
          value={lender}
          onChange={(e) => setLender(e.target.value)}
          placeholder="Bank, a friend, anyone"
          className={field}
        />
      </label>

      <div className="flex flex-wrap gap-3">
        <label className="flex min-w-40 flex-1 flex-col gap-1">
          <span className="text-xs font-medium text-ink-soft">How much did you borrow?</span>
          <AmountField value={amount} onChange={setAmount} placeholder="12000" className={field} />
        </label>

        <label className="flex flex-col gap-1">
          <span className="text-xs font-medium text-ink-soft">When?</span>
          <input
            type="date"
            value={takenOn}
            onChange={(e) => setTakenOn(e.target.value)}
            className={field}
          />
        </label>
      </div>

      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">Why did you take it? (optional)</span>
        <input
          value={remark}
          onChange={(e) => setRemark(e.target.value)}
          placeholder="The thing you will have forgotten in a year"
          className={field}
        />
      </label>

      <div className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">Which heads repay it?</span>
        <p className="text-xs text-ink-muted">
          <strong className="font-medium text-ink-soft">Every</strong> expense you log on
          these counts against the loan, so don't use them for anything else. A head can
          only belong to one loan.
        </p>
        <HeadMultiSelect
          categories={categories ?? []}
          value={headIds}
          onChange={setHeadIds}
          emptyHint="Nothing linked yet — the loan will sit at its full amount until you link a head."
        />
        <LinkedHeadWarning
          headIds={headIds}
          from={takenOn}
          ledger="Expense"
          categories={categories ?? []}
          counts="count as repayments"
        />
      </div>

      {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}

      <div className="flex gap-2">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving…' : loan ? 'Save changes' : 'Add loan'}
        </Button>
        <Button type="button" variant="ghost" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  )
}
