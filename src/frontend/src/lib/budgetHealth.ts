/**
 * How the budget someone set compares with the income they have actually earned.
 *
 * Income arrives through the period while the budget is set once at the start, so this
 * reading climbs as the period goes on: it opens short of the budget and works its way
 * up as money comes in. That is the point of showing it — the same figure means
 * something different on the 2nd than it does on the 28th.
 *
 * Kept apart from the component that draws it so the thresholds are one list to edit
 * and the wording never has to be untangled from the arithmetic.
 */

export type BudgetHealthTone =
  /** Nothing budgeted, so there is nothing to compare income against. */
  | 'none'
  /** The budget outruns the income earned so far. */
  | 'short'
  /** Income has reached the budget. */
  | 'covered'
  /** Income has run comfortably past it. */
  | 'surplus'
  /** Income has run well past it. */
  | 'gold'

export interface BudgetHealth {
  tone: BudgetHealthTone
  /** Income minus budget. Negative while the budget is the bigger number. */
  deviation: number
  /** How far past the budget income has reached, as a whole percent. 0 below it. */
  surplusPercent: number
}

/**
 * The rungs, as whole percents clear of the budget — ordered high to low, so the first
 * one income reaches is the one it gets. This list is the whole ladder: add a rung here,
 * give it a colour and a sentence in `PeriodOverview`, and nothing else needs touching.
 */
const TIERS: ReadonlyArray<{ readonly clearBy: number; readonly tone: BudgetHealthTone }> = [
  { clearBy: 50, tone: 'gold' },
  { clearBy: 20, tone: 'surplus' },
  { clearBy: 0, tone: 'covered' },
]

export function readBudgetHealth(totalBudget: number, totalIncome: number): BudgetHealth {
  // Amounts are stored to two decimals, so whole cents are the exact unit to compare in.
  // Dividing one amount by the other instead leaves a figure that is precisely 50% clear
  // sitting a hair *under* the rung it earned — 185,185.17 against 123,456.78 did.
  const budget = Math.round(totalBudget * 100)
  const income = Math.round(totalIncome * 100)
  const deviation = (income - budget) / 100

  // No budget is not a failing grade — there is simply no ratio to take, and dividing by
  // it would hand the screen a NaN percentage.
  if (budget <= 0) {
    return { tone: 'none', deviation, surplusPercent: 0 }
  }

  const tone =
    TIERS.find((tier) => income * 100 >= budget * (100 + tier.clearBy))?.tone ?? 'short'

  return {
    tone,
    surplusPercent: Math.max(0, Math.round(((income - budget) / budget) * 100)),
    deviation,
  }
}
