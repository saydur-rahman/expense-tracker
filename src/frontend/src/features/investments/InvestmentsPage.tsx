import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  investmentsApi,
  type Investment,
  type InvestmentGroupTotals,
  type InvestmentKind,
} from '../../api/investments'
import { categoriesApi } from '../../api/categories'
import { budgetPeriodsApi } from '../../api/settings'
import { ApiError } from '../../api/client'
import { useMoney } from '../../lib/money'
import Button from '../../components/Button'
import HeadMultiSelect from '../../components/HeadMultiSelect'
import LinkedHeadWarning from '../../components/LinkedHeadWarning'
import PeriodPicker from '../../components/PeriodPicker'
import ProgressBar from '../../components/charts/ProgressBar'
import TwoSliceDonut from '../../components/charts/TwoSliceDonut'
import LegendRow from '../../components/charts/LegendRow'
import { LEFT_COLOR, SPENT_COLOR } from '../../components/charts/colors'
import { card, emptyState, field, eyebrow, pageTitle } from '../../components/ui'
import { todayLocal } from '../../lib/dates'
import { wordingFor } from './wording'

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

  const { data: portfolio } = useQuery({
    queryKey: ['investments', 'portfolio', period?.id],
    queryFn: () => investmentsApi.portfolio(period!.id),
    enabled: !!period,
  })

  const { data: vsIncome } = useQuery({
    queryKey: ['investments', 'vs-income', period?.id],
    queryFn: () => investmentsApi.vsIncome(period!.id),
    enabled: !!period,
  })

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className={pageTitle}>Investments &amp; lending</h1>
        <p className="text-sm text-ink-muted">
          What you have put out, and how much of it has come back.
        </p>
      </div>

      <PeriodPicker label={period?.label ?? '…'} offset={offset} onOffsetChange={setOffset} />

      {portfolio?.groups
        .filter((g) => g.count > 0)
        .map((g) => <GroupTotals key={g.kind} group={g} periodLabel={portfolio.periodLabel} />)}

      {vsIncome && <VsIncome data={vsIncome} />}

      {adding ? (
        <InvestmentForm onDone={() => setAdding(false)} />
      ) : (
        <Button onClick={() => setAdding(true)}>Add an investment</Button>
      )}

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      {investments?.length === 0 && (
        <p className={emptyState}>
          Nothing here yet. Add an investment or something you have lent out, link the heads
          the money leaves through and the head it comes back on, and both sides will fill in
          as you log them.
        </p>
      )}

      {(['Investment', 'Lend'] as const).map((kind) => {
        const group = investments?.filter((i) => i.kind === kind) ?? []
        if (group.length === 0) return null

        return (
          <section key={kind} className="flex flex-col gap-2">
            {/* Only worth a heading once both kinds are actually in use. */}
            {investments!.some((i) => i.kind !== kind) && (
              <h2 className={eyebrow}>{wordingFor(kind).group}</h2>
            )}
            {group.map((investment) => (
              <InvestmentCard key={investment.id} investment={investment} />
            ))}
          </section>
        )
      })}
    </div>
  )
}

/**
 * One kind added up. The period picker changes only the bottom line — what you have out is
 * what you have out, whichever cycle you are looking at.
 */
function GroupTotals({
  group,
  periodLabel,
}: {
  group: InvestmentGroupTotals
  periodLabel: string
}) {
  const { format } = useMoney()
  const words = wordingFor(group.kind)

  return (
    <section className={`${card} p-4`}>
      <h2 className={`${eyebrow} mb-2`}>
        {words.group} ({group.count})
      </h2>

      <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-2">
        <span>
          <span className={`${eyebrow} block`}>{words.remaining}</span>
          <span className="block text-xl font-semibold tabular-nums text-negative-600 dark:text-negative-400">
            {format(group.outstanding)}
          </span>
        </span>
        <span className="text-right">
          <span className={`${eyebrow} block`}>{words.out}</span>
          <span className="block text-lg font-semibold tabular-nums text-ink">
            {format(group.out)}
          </span>
        </span>
        <span className="text-right">
          <span className={`${eyebrow} block`}>{words.back}</span>
          <span className="block text-lg font-semibold tabular-nums text-positive-700 dark:text-positive-400">
            {format(group.back)}
          </span>
        </span>
      </div>

      <ProgressBar
        value={group.back}
        total={group.out}
        fill="bg-positive-600"
        overFill="bg-positive-600"
      />

      <p className="mt-2 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 border-t border-line-soft pt-2 text-xs text-ink-muted">
        <span>
          {group.percentBack}% back
          {group.recoupedCount > 0 &&
            ` · ${group.recoupedCount} of ${group.count} ${words.settled.toLowerCase()}`}
          {group.surplus > 0 && ` · ${format(group.surplus)} ${words.surplus.toLowerCase()}`}
        </span>
        <span>
          In {periodLabel}{' '}
          <strong className="font-semibold tabular-nums text-ink">
            {format(group.outInPeriod)}
          </strong>{' '}
          out,{' '}
          <strong className="font-semibold tabular-nums text-ink">
            {format(group.backInPeriod)}
          </strong>{' '}
          back
        </span>
      </p>
    </section>
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
  const words = wordingFor(investment.kind)

  return (
    <Link
      to={`/investments/${investment.id}`}
      className={`${card} block px-4 py-3 transition-colors hover:bg-raised`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="min-w-0">
          <span className="block truncate font-medium text-ink">{investment.name}</span>
          {investment.counterparty && (
            <span className="block truncate text-xs text-ink-muted">
              {investment.counterparty}
            </span>
          )}
        </span>
        <span className="shrink-0 text-right">
          <span
            className={`block text-sm font-semibold tabular-nums ${
              investment.isRecouped
                ? 'text-positive-700 dark:text-positive-400'
                : 'text-negative-600 dark:text-negative-400'
            }`}
          >
            {investment.isRecouped ? words.settled : format(investment.outstanding)}
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
          No head linked for money going out — nothing will count until you link one.
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
  const [kind, setKind] = useState<InvestmentKind>(investment?.kind ?? 'Investment')
  const [counterparty, setCounterparty] = useState(investment?.counterparty ?? '')
  const [startedOn, setStartedOn] = useState(investment?.startedOn ?? todayLocal())
  const [remark, setRemark] = useState(investment?.remark ?? '')
  const [contributionHeadIds, setContributionHeadIds] = useState<string[]>(
    investment?.contributionHeads.map((h) => h.headId) ?? [],
  )
  const [returnHeadIds, setReturnHeadIds] = useState<string[]>(
    investment?.returnHeads.map((h) => h.headId) ?? [],
  )
  const [error, setError] = useState<string | null>(null)

  const words = wordingFor(kind)

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
        kind,
        counterparty: kind === 'Lend' ? counterparty.trim() || null : null,
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
      {/* The kind is chosen first because every label below it changes. */}
      <div className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">Which is it?</span>
        <div role="tablist" className="flex gap-1 rounded-lg bg-raised p-1">
          {(['Investment', 'Lend'] as const).map((option) => (
            <button
              key={option}
              type="button"
              role="tab"
              aria-selected={kind === option}
              onClick={() => setKind(option)}
              className={`flex-1 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
                kind === option
                  ? 'bg-card text-brand-700 shadow-sm dark:text-brand-300'
                  : 'text-ink-muted hover:text-ink'
              }`}
            >
              {option === 'Lend' ? 'Money I lent out' : 'An investment'}
            </button>
          ))}
        </div>
      </div>

      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">{words.nameLabel}</span>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={words.namePlaceholder}
          className={field}
        />
      </label>

      {kind === 'Lend' && (
        <label className="flex flex-col gap-1">
          <span className="text-xs font-medium text-ink-soft">Who did you lend it to?</span>
          <input
            value={counterparty}
            onChange={(e) => setCounterparty(e.target.value)}
            placeholder="A name, so you remember who owes you"
            className={field}
          />
        </label>
      )}

      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">
          {kind === 'Lend' ? 'Lent when?' : 'Started when?'}
        </span>
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
        <span className="text-xs font-medium text-ink-soft">{words.outHeads}</span>
        <p className="text-xs text-ink-muted">{words.outHeadsHint}</p>
        <HeadMultiSelect
          categories={expenseCategories ?? []}
          value={contributionHeadIds}
          onChange={setContributionHeadIds}
          placeholder="Add a spending head…"
          emptyHint={words.outHeadsEmpty}
        />
        <LinkedHeadWarning
          headIds={contributionHeadIds}
          from={startedOn}
          ledger="Expense"
          categories={expenseCategories ?? []}
          counts={kind === 'Lend' ? 'count as money lent' : 'count as money invested'}
        />
      </div>

      <div className="flex flex-col gap-1">
        <span className="text-xs font-medium text-ink-soft">{words.backHeads}</span>
        <p className="text-xs text-ink-muted">{words.backHeadsHint}</p>
        <HeadMultiSelect
          categories={incomeCategories ?? []}
          value={returnHeadIds}
          onChange={setReturnHeadIds}
          placeholder="Add an income head…"
          emptyHint={words.backHeadsEmpty}
        />
        <LinkedHeadWarning
          headIds={returnHeadIds}
          from={startedOn}
          ledger="Income"
          categories={incomeCategories ?? []}
          counts={kind === 'Lend' ? 'count as repayments to you' : 'count as returns'}
        />
      </div>

      {error && <p className="text-sm text-negative-600 dark:text-negative-400">{error}</p>}

      <div className="flex gap-2">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending
            ? 'Saving…'
            : investment
              ? 'Save changes'
              : kind === 'Lend'
                ? 'Add it'
                : 'Add investment'}
        </Button>
        <Button type="button" variant="ghost" onClick={onDone}>
          Cancel
        </Button>
      </div>
    </form>
  )
}
