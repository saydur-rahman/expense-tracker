import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { reportsApi, type CategorySummary, type PeriodSummary } from '../api/reports'
import type { CategoryKind } from '../api/categories'
import LedgerTabs from '../components/LedgerTabs'
import PeriodPicker from '../components/PeriodPicker'
import PeriodOverview from '../components/PeriodOverview'
import { budgetPeriodsApi } from '../api/settings'
import { useMoney } from '../lib/money'

// The app's three colours, used here as chart marks. Validated against both card
// card surfaces (#f8fafc light, #141d2e dark) for lightness band, chroma, colour-blind separation
// and 3:1 contrast — so one pair serves both themes.
// Green is consistently "money you still have", blue "money that has gone out",
// red "trouble".
const SPENT_COLOR = '#2563eb'
const LEFT_COLOR = '#16a34a'
const OVER_COLOR = '#dc2626'

export default function DashboardPage() {
  const [ledger, setLedger] = useState<CategoryKind>('Expense')
  const [offset, setOffset] = useState(0)

  // Resolved rather than summarised directly: the summary hangs off a period row, and
  // stepping to a cycle never opened before is what creates it.
  const { data: period } = useQuery({
    queryKey: ['budget-period', offset],
    queryFn: () => budgetPeriodsApi.relative(offset),
  })

  const { data: summary, isLoading } = useQuery({
    queryKey: ['summary', period?.id],
    queryFn: () => reportsApi.summary(period!.id),
    enabled: !!period,
  })

  const isIncome = ledger === 'Income'
  const breakdown = isIncome ? (summary?.incomeCategories ?? []) : (summary?.categories ?? [])

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight text-ink">Dashboard</h1>

      <PeriodPicker label={period?.label ?? '…'} offset={offset} onOffsetChange={setOffset} />

      {/* Above the tabs on purpose: these four figures describe the period itself, so
          they must not move or change when the breakdown below switches ledger. */}
      {summary && <PeriodOverview summary={summary} kind={period?.kind ?? 'Month'} />}

      {/* The tab picks the breakdown — chart and categories both follow it. */}
      <LedgerTabs value={ledger} onChange={setLedger} />

      {isLoading && <p className="text-ink-muted">Loading…</p>}

      {summary && (isIncome ? <IncomeTotals summary={summary} /> : <MonthTotals summary={summary} />)}

      {summary && breakdown.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line p-8 text-center text-sm text-ink-muted">
          {isIncome
            ? 'No income in this period. Add an income category, then log what came in.'
            : 'Nothing to show for this period. Add a category, budget a head, then log an expense.'}
        </p>
      ) : (
        <div className="flex flex-col gap-2">
          {breakdown.map((category) => (
            <CategoryCard
              key={category.categoryId}
              category={category}
              // With only a handful of categories there is nothing to tidy away;
              // past that, start collapsed so the whole month fits on one screen.
              defaultOpen={breakdown.length <= 4}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function MonthTotals({ summary }: { summary: PeriodSummary }) {
  const { format } = useMoney()
  const { totalBudget: budget, totalSpent: spent, totalRemaining: remaining } = summary
  const over = remaining < 0
  const pctUsed = budget > 0 ? (spent / budget) * 100 : 0

  // The budget and spent figures themselves live in the overview strip above; what is
  // left to add here is their shape.
  return (
    <div className="rounded-xl border border-line bg-card p-4 shadow-sm">
      {budget > 0 ? (
        <>
          <TotalBar spent={spent} budget={budget} />
          <p className="mt-1.5 text-xs text-ink-muted">
            {over
              ? `Over budget by ${format(Math.abs(remaining))}`
              : `${Math.round(pctUsed)}% used`}
          </p>

          <div className="mt-4 flex items-center gap-4">
            <TwoSliceDonut
              first={{ label: 'Spent', value: spent, color: over ? OVER_COLOR : SPENT_COLOR }}
              second={{ label: 'Left', value: Math.max(0, remaining), color: LEFT_COLOR }}
            />
            <dl className="flex min-w-0 flex-1 flex-col gap-2">
              <LegendRow
                swatch={over ? OVER_COLOR : SPENT_COLOR}
                label="Spent" value={format(spent)}
              />
              <LegendRow
                swatch={over ? OVER_COLOR : LEFT_COLOR}
                label={over ? 'Over by' : 'Left'}
                value={format(Math.abs(remaining))}
                emphasis
              />
            </dl>
          </div>
        </>
      ) : (
        <p className="text-sm text-ink-muted">
          Set a category budget to track how much is left.
        </p>
      )}
    </div>
  )
}

function IncomeTotals({ summary }: { summary: PeriodSummary }) {
  const { format } = useMoney()
  const { totalIncome: income, totalSpent: spent, totalSaved: saved } = summary
  const overspent = saved < 0
  const hasAnything = income > 0 || spent > 0

  // Income and what is left of it are in the overview strip above; this card is the
  // shape of the split.
  return (
    <div className="rounded-xl border border-line bg-card p-4 shadow-sm">
      {hasAnything ? (
        <>
          <p className="text-xs text-ink-muted">
            {overspent
              ? `Spent ${format(Math.abs(saved))} more than you earned`
              : `Kept ${income > 0 ? Math.round((saved / income) * 100) : 0}% of it`}
          </p>

          <div className="mt-4 flex items-center gap-4">
            {/* The whole ring is your income: expense takes a slice and whatever is
                left over is savings. Outspend your income and there is no slice
                left, so the ring goes fully red. */}
            <TwoSliceDonut
              first={{ label: 'Expense', value: spent, color: overspent ? OVER_COLOR : SPENT_COLOR }}
              second={{ label: 'Savings', value: Math.max(0, saved), color: LEFT_COLOR }}
            />
            <dl className="flex min-w-0 flex-1 flex-col gap-2">
              <LegendRow label="Income" value={format(income)} />
              <LegendRow
                swatch={overspent ? OVER_COLOR : SPENT_COLOR}
                label="Expense" value={format(spent)}
              />
              <LegendRow
                swatch={overspent ? OVER_COLOR : LEFT_COLOR}
                label={overspent ? 'Overspent' : 'Savings'}
                value={format(Math.abs(saved))}
                emphasis
              />
            </dl>
          </div>
        </>
      ) : (
        <p className="text-sm text-ink-muted">
          Log some income to see how much of it you are keeping.
        </p>
      )}
    </div>
  )
}

function TotalBar({ spent, budget }: { spent: number; budget: number }) {
  const over = spent > budget
  const spentPct = over ? 100 : (spent / budget) * 100
  const leftPct = 100 - spentPct

  // The flex gap lets the card surface show between the two fills, so the boundary
  // reads without leaning on the colours alone.
  return (
    <div className="mt-2 flex h-2.5 gap-0.5 overflow-hidden rounded-full bg-track">
      {spentPct > 0 && (
        <div
          className="h-full rounded-full transition-all" style={{ width: `${spentPct}%`, backgroundColor: over ? OVER_COLOR : SPENT_COLOR }}
        />
      )}
      {leftPct > 0 && (
        <div
          className="h-full rounded-full transition-all" style={{ width: `${leftPct}%`, backgroundColor: LEFT_COLOR }}
        />
      )}
    </div>
  )
}

interface Slice {
  label: string
  value: number
  color: string
}

/**
 * A thin two-slice ring with an open centre — the figures live in the legend
 * beside it, so nothing has to be crammed into the hole.
 */
function TwoSliceDonut({ first, second }: { first: Slice; second: Slice }) {
  const { format } = useMoney()

  const size = 108
  const radius = 46
  const stroke = 10
  const middle = size / 2
  const circumference = 2 * Math.PI * radius

  const total = first.value + second.value
  const firstFraction = total > 0 ? first.value / total : 0

  // Only split the ring when both slices exist; a lone full ring shouldn't
  // carry a phantom gap.
  const split = first.value > 0 && second.value > 0
  const gap = split ? 2 : 0

  const firstLength = Math.max(0, firstFraction * circumference - gap)
  const secondLength = Math.max(0, (1 - firstFraction) * circumference - gap)

  const label = `${first.label} ${format(first.value)}, ${second.label} ${format(second.value)}.`

  return (
    <svg
      width={size}
      height={size}
      viewBox={`0 0 ${size} ${size}`}
      role="img" aria-label={label}
      className="shrink-0"
    >
      <g transform={`rotate(-90 ${middle} ${middle})`} fill="none" strokeWidth={stroke}>
        {firstLength > 0 && (
          <circle
            cx={middle}
            cy={middle}
            r={radius}
            stroke={first.color}
            strokeDasharray={`${firstLength} ${circumference - firstLength}`}
            strokeDashoffset={-gap / 2}
          >
            <title>{`${first.label} ${format(first.value)}`}</title>
          </circle>
        )}
        {secondLength > 0 && (
          <circle
            cx={middle}
            cy={middle}
            r={radius}
            stroke={second.color}
            strokeDasharray={`${secondLength} ${circumference - secondLength}`}
            strokeDashoffset={-(firstFraction * circumference + gap / 2)}
          >
            <title>{`${second.label} ${format(second.value)}`}</title>
          </circle>
        )}
      </g>
    </svg>
  )
}

function LegendRow({
  swatch,
  label,
  value,
  emphasis,
}: {
  /** Omitted for a row that is the ring's total rather than one of its slices. */
  swatch?: string
  label: string
  value: string
  emphasis?: boolean
}) {
  return (
    <div className="flex items-baseline justify-between gap-2">
      <dt className="flex items-center gap-2 text-sm text-ink-soft">
        {swatch ? (
          <span
            aria-hidden="true" className="inline-block size-2.5 shrink-0 rounded-full" style={{ backgroundColor: swatch }}
          />
        ) : (
          // Keeps the labels aligned with the rows that do carry a swatch.
          <span aria-hidden="true" className="inline-block size-2.5 shrink-0" />
        )}
        {label}
      </dt>
      <dd
        className={`text-sm tabular-nums ${
 emphasis
            ? 'font-semibold text-ink'
            : 'text-ink-soft'
        }`}
      >
        {value}
      </dd>
    </div>
  )
}

function CategoryCard({
  category,
  defaultOpen,
}: {
  category: CategorySummary
  defaultOpen: boolean
}) {
  const { format } = useMoney()
  const [open, setOpen] = useState(defaultOpen)
  const headCount = category.heads.length

  return (
    <div className="overflow-hidden rounded-xl border border-line bg-card shadow-sm">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className="w-full px-4 py-3 text-left transition-colors hover:bg-raised"
      >
        <div className="flex items-baseline justify-between gap-2">
          <span className="flex min-w-0 items-baseline gap-2">
            <span
              aria-hidden="true"
              className={`shrink-0 text-xs text-ink-muted transition-transform ${open ? 'rotate-90' : ''}`}
            >
              ▶
            </span>
            <span className="truncate font-medium text-ink">
              {category.categoryName}
              {category.isArchived && (
                <span className="ml-2 text-xs font-normal text-ink-muted">removed</span>
              )}
            </span>
          </span>
          <span className="shrink-0 text-sm tabular-nums text-ink-muted">
            {format(category.spent)}
            {category.budget !== null && ` / ${format(category.budget)}`}
          </span>
        </div>

        <ProgressBar spent={category.spent} budget={category.budget} />

        {!open && headCount > 0 && (
          <p className="mt-1.5 text-xs text-ink-muted">
            {headCount} {headCount === 1 ? 'head' : 'heads'}
          </p>
        )}
      </button>

      {open && headCount > 0 && (
        <ul className="flex flex-col gap-2 border-t border-line-soft px-4 py-3">
          {category.heads.map((head) => (
            <li key={head.headId}>
              <div className="flex items-baseline justify-between gap-2">
                <span className="truncate text-sm text-ink-soft">
                  {head.headName}
                  {head.isArchived && <span className="ml-2 text-xs text-ink-muted">removed</span>}
                </span>
                <span
                  className={`shrink-0 text-sm tabular-nums ${
                    head.isOverBudget ? 'text-negative-600 dark:text-negative-400' : 'text-ink-muted'
                  }`}
                >
                  {format(head.spent)}
                  {head.budget !== null && ` / ${format(head.budget)}`}
                </span>
              </div>
              <ProgressBar spent={head.spent} budget={head.budget} thin />
            </li>
          ))}
        </ul>
      )}
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
    <div className={`mt-1.5 overflow-hidden rounded-full bg-track ${thin ? 'h-1' : 'h-2'}`}>
      <div
        className={`h-full transition-all ${over ? 'bg-negative-500' : 'bg-brand-500'}`}
        style={{ width: `${pct}%` }}
      />
    </div>
  )
}
