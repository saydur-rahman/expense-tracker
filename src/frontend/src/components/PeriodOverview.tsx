import type { ReactNode } from 'react'
import type { PeriodSummary } from '../api/reports'
import type { PeriodKind } from '../api/settings'
import { readBudgetHealth, type BudgetHealthTone } from '../lib/budgetHealth'
import { useMoney } from '../lib/money'
import Bar from './charts/Bar'
import { card } from './ui'

/**
 * The period read as four bars on one scale: what you planned to spend, what has come
 * in, what has gone out, what that leaves you.
 *
 * **The budget is the base.** Every bar is drawn against the same scale and a budget line
 * runs down all four at the same place, so income falling short of the budget or running
 * past it is something you see rather than something you work out. The line is drawn over
 * the fills, so a bar that beats the budget visibly crosses it.
 *
 * It sits above the Expense/Income tabs and is deliberately outside them — switching tabs
 * changes the breakdown underneath, never this. Both the dashboard and the Budgets screen
 * render it from the same summary so the two can't disagree.
 */
export default function PeriodOverview({
  summary,
  kind,
}: {
  summary: PeriodSummary
  kind: PeriodKind
}) {
  const { format } = useMoney()
  const { totalBudget, totalIncome, totalSpent, totalSaved } = summary

  const span = kind === 'Week' ? 'this week' : 'this month'
  const health = readBudgetHealth(totalBudget, totalIncome)
  const covered = health.tone !== 'short' && health.tone !== 'none'
  const overspent = totalSaved < 0

  // One scale for all four, so their lengths are comparable and the budget line lands in
  // the same place on each. Left is taken by magnitude: an overspend draws a short red
  // stub rather than nothing at all.
  const scale = Math.max(totalBudget, totalIncome, totalSpent, Math.abs(totalSaved))
  const width = (value: number) => (scale > 0 ? (Math.abs(value) / scale) * 100 : 0)
  const budgetMark = scale > 0 && totalBudget > 0 ? width(totalBudget) : null

  return (
    <section className={`${card} flex flex-col gap-3 p-4`} aria-label="Period overview">
      <Bar
        label="Budget"
        value={format(totalBudget)}
        width={width(totalBudget)}
        fill={covered ? 'bg-positive-600' : 'bg-negative-500'}
        mark={budgetMark}
        note={
          totalBudget === 0 ? (
            <Note className="text-ink-muted">Nothing budgeted for {span} yet.</Note>
          ) : covered ? (
            <Note className="text-positive-700 dark:text-positive-400">
              Your income covers this.
            </Note>
          ) : (
            <Note className="text-negative-600 dark:text-negative-400">
              More than you have earned {span}.
            </Note>
          )
        }
      />

      <Bar
        label="Income"
        value={format(totalIncome)}
        width={width(totalIncome)}
        fill={INCOME_FILL[health.tone]}
        mark={budgetMark}
        note={<IncomeNote tone={health.tone} health={health} format={format} />}
      />

      {/* Spent and Left carry no state of their own — the two bars above are where the
          colour means something, and a third and fourth signal would drown them out. */}
      <Bar
        label="Spent"
        value={format(totalSpent)}
        width={width(totalSpent)}
        fill="bg-brand-600"
        mark={budgetMark}
      />

      <Bar
        label="Left"
        value={format(totalSaved)}
        // Left is income minus spending, not budget minus spending — money actually still
        // in hand. The caption is here because those two readings differ.
        caption="of your income"
        width={width(totalSaved)}
        // The one exception to Left being static: outspending what you earned is worth
        // breaking the rule for.
        fill={overspent ? 'bg-negative-500' : 'bg-ink-muted'}
        mark={budgetMark}
      />

      {totalIncome === 0 && (
        <p className="text-xs text-ink-muted">
          No income logged for this period yet — log it on the Income screen and these fill in.
        </p>
      )}
    </section>
  )
}

function Note({ className, children }: { className: string; children: ReactNode }) {
  return <p className={`text-[11px] leading-tight ${className}`}>{children}</p>
}

const INCOME_FILL: Record<BudgetHealthTone, string> = {
  none: 'bg-ink-muted',
  short: 'bg-negative-500',
  covered: 'bg-positive-600',
  surplus: 'bg-surplus-400',
  gold: 'bg-gold-400',
}

/** Pills for the two top rungs: yellow and gold cannot reach 4.5:1 as ink on a light card. */
const RUNG_PILL: Partial<Record<BudgetHealthTone, { pill: string; icon: string }>> = {
  surplus: {
    pill: 'bg-surplus-100 text-surplus-800 dark:bg-surplus-950 dark:text-surplus-300',
    icon: '▲',
  },
  gold: {
    pill: 'bg-gold-100 text-gold-800 ring-1 ring-gold-400 dark:bg-gold-950 dark:text-gold-300',
    icon: '★',
  },
}

function IncomeNote({
  tone,
  health,
  format,
}: {
  tone: BudgetHealthTone
  health: ReturnType<typeof readBudgetHealth>
  format: (value: number) => string
}) {
  if (tone === 'none') return null

  const rung = RUNG_PILL[tone]
  if (rung) {
    return (
      <p className="mt-1">
        <span
          className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ${rung.pill}`}
        >
          <span aria-hidden="true">{rung.icon}</span>
          {health.surplusPercent}% clear of your budget
        </span>
      </p>
    )
  }

  if (tone === 'short') {
    return (
      <Note className="text-negative-600 dark:text-negative-400">
        {format(Math.abs(health.deviation))} short of your budget.
      </Note>
    )
  }

  return (
    <Note className="text-positive-700 dark:text-positive-400">
      {health.deviation === 0
        ? 'Exactly matches your budget.'
        : `Covers your budget, with ${format(health.deviation)} to spare.`}
    </Note>
  )
}
