import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { investmentsApi, type Investment } from '../../api/investments'
import { categoriesApi } from '../../api/categories'
import { budgetPeriodsApi } from '../../api/settings'
import { ApiError } from '../../api/client'
import { useMoney } from '../../lib/money'
import Button from '../../components/Button'
import HeadMultiSelect from '../../components/HeadMultiSelect'
import PeriodPicker from '../../components/PeriodPicker'
import ProgressBar from '../../components/charts/ProgressBar'
import TwoSliceDonut from '../../components/charts/TwoSliceDonut'
import LegendRow from '../../components/charts/LegendRow'
import { LEFT_COLOR, SPENT_COLOR } from '../../components/charts/colors'
import { card, emptyState, field, eyebrow, pageTitle } from '../../components/ui'

const today = () => new Date().toISOString().slice(0, 10)

export default function InvestmentsPage() {
  const [adding, setAdding] = useState(false)
  const [offset, setOffset] = useState(0)

  const { data: investments, isLoading } = useQuery({
    queryKey: ['investments'],
    queryFn: investmentsApi.list,
  })

  const { data: period } = useQuery({
    queryKey: ['budget-period', offset],
    queryFn: () => budgetPeriodsApi.relative(offset),
  })

  const { data: vsIncome } = useQuery({
    queryKey: ['investment-vs-income', period?.id],
    queryFn: () => investmentsApi.vsIncome(period!.id),
    enabled: !!period,
  })

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className={pageTitle}>Investments</h1>
        <p className="text-sm text-ink-muted">
          What you have put in, and how much of it has come back.
        </p>
      </div>

      <PeriodPicker label={period?.label ?? '…'} offset={offset} onOffsetChange={setOffset} />

      {vsIncome && <VsIncome data={vsIncome} />}

      {adding ? (
        <InvestmentForm onDone={() => setAdding(false)} />
      ) : (
        <Button onClick={() => setAdding(true)}>Add an investment</Button>
      )}

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      {investments?.length === 0 && (
        <p className={emptyState}>
          Nothing here yet. Add an investment, link the heads you pay into it through and the
          head your returns arrive on, and both sides will fill in as you log them.
        </p>
      )}

      <div className="flex flex-col gap-2">
        {investments?.map((investment) => (
          <InvestmentCard key={investment.id} investment={investment} />
        ))}
      </div>
    </div>
  )
}

/** How much of what you earned this cycle went into investments. */
function VsIncome({
  data,
}: {
  data: { invested: number; income: number; remainder: number; percentOfIncome: number }
}) {
  const { format } = useMoney()
  const overInvested = data.remainder < 0

  if (data.income === 0 && data.invested === 0) {
    return (
      <p className={`${card} p-4 text-sm text-ink-muted`}>
        Nothing earned or invested in this cycle yet.
      </p>
    )
  }

  return (
    <section className={`${card} p-4`}>
      <h2 className={`${eyebrow} mb-3`}>Invested against income</h2>
      <div className="flex items-center gap-4">
        <TwoSliceDonut
          first={{ label: 'Invested', value: data.invested, color: SPENT_COLOR }}
          second={{ label: 'Rest of your income', value: Math.max(0, data.remainder), color: LEFT_COLOR }}
        />
        <dl className="flex min-w-0 flex-1 flex-col gap-2">
          <LegendRow label="Income" value={format(data.income)} />
          <LegendRow swatch={SPENT_COLOR} label="Invested" value={format(data.invested)} emphasis />
          <LegendRow
            swatch={LEFT_COLOR}
            label={overInvested ? 'Over your income by' : 'Rest'}
            value={format(Math.abs(data.remainder))}
          />
        </dl>
      </div>
      <p className="mt-3 border-t border-line-soft pt-3 text-xs text-ink-muted">
        {overInvested
          ? 'You put in more than you earned this cycle — the difference came from somewhere else.'
          : `${data.percentOfIncome}% of what you earned this cycle went into investments.`}
      </p>
    </section>
  )
}

function InvestmentCard({ investment }: { investment: Investment }) {
  const { format } = useMoney()

  return (
    <Link
      to={`/investments/${investment.id}`}
      className={`${card} block px-4 py-3 transition-colors hover:bg-raised`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="min-w-0 truncate font-medium text-ink">{investment.name}</span>
        <span className="shrink-0 text-right">
          <span
            className={`block text-sm font-semibold tabular-nums ${
              investment.isRecouped
                ? 'text-positive-700 dark:text-positive-400'
                : 'text-negative-600 dark:text-negative-400'
            }`}
          >
            {investment.isRecouped ? 'Recouped' : format(investment.outstanding)}
          </span>
          <span className="block text-xs text-ink-muted">
            {format(investment.returned)} back of {format(investment.invested)}
          </span>
        </span>
      </div>

      <ProgressBar
        value={investment.returned}
        total={investment.invested}
        fill="bg-positive-600"
        overFill="bg-positive-600"
      />

      {investment.contributionHeads.length === 0 && (
        <p className="mt-1.5 text-xs text-negative-600 dark:text-negative-400">
          No head linked for money going in — nothing will count until you link one.
        </p>
      )}
    </Link>
  )
}

export function InvestmentForm({
  investment,
  onDone,
}: {
  investment?: Investment
  onDone: () => void
}) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(investment?.name ?? '')
  const [startedOn, setStartedOn] = useState(investment?.startedOn ?? today())
  const [remark, setRemark] = useState(investment?.remark ?? '')
  const [contributionHeadIds, setContributionHeadIds] = useState<string[]>(
    investment?.contributionHeads.map((h) => h.headId) ?? [],
  )
  const [returnHeadIds, setReturnHeadIds] = useState<string[]>(
    investment?.returnHeads.map((h) => h.headId) ?? [],
  )
  const [error, setError] = useState<string | null>(null)

  // The two sides come from the two ledgers: money in is spending, money back is income.
  const { data: expenseCategories } = useQuery({
    queryKey: ['categories', 'Expense'],
    queryFn: () => categoriesApi.list('Expense'),
  })
  const { data: incomeCategories } = useQuery({
    queryKey: ['categories', 'Income'],
    queryFn: () => categoriesApi.list('Income'),
  })

  const mutation = useMutation({
    mutationFn: () => {
      const request = {
        name: name.trim(),
        remark: remark.trim() || null,
        startedOn,
        contributionHeadIds,
        returnHeadIds,
      }
      return investment
        ? investmentsApi.update(investment.id, request)
        : investmentsApi.create(request)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['investments'] })
      if (investment) queryClient.invalidateQueries({ queryKey: ['investment', investment.id] })
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
          placeholder="Shares, a shop, a plot of land"
          className={field}
        />
      </label>

      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">Started when?</span>
        <input
          type="date"
          value={startedOn}
          onChange={(e) => setStartedOn(e.target.value)}
          className={field}
        />
      </label>

      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">Why? (optional)</span>
        <input
          value={remark}
          onChange={(e) => setRemark(e.target.value)}
          placeholder="What you were hoping for"
          className={field}
        />
      </label>

      <div className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">Where the money goes in</span>
        <p className="text-xs text-ink-muted">
          Spending heads. <strong className="font-medium text-ink-soft">Every</strong> expense
          on these counts as money invested, so don't use them for anything else.
        </p>
        <HeadMultiSelect
          categories={expenseCategories ?? []}
          value={contributionHeadIds}
          onChange={setContributionHeadIds}
          placeholder="Add a spending head…"
          emptyHint="Nothing linked yet — no money will be counted as invested."
        />
      </div>

      <div className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">Where the returns come back</span>
        <p className="text-xs text-ink-muted">
          Income heads. Anything you log on these counts as money coming back out of it.
        </p>
        <HeadMultiSelect
          categories={incomeCategories ?? []}
          value={returnHeadIds}
          onChange={setReturnHeadIds}
          placeholder="Add an income head…"
          emptyHint="Nothing linked yet — returns won't be counted until you link one."
        />
      </div>

      {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}

      <div className="flex gap-2">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? 'Saving…' : investment ? 'Save changes' : 'Add investment'}
        </Button>
        <Button type="button" variant="ghost" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  )
}
