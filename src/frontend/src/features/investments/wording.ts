import type { InvestmentKind } from '../../api/investments'

/**
 * Investing and lending are the same arithmetic — money out, money back, and a ring
 * showing how much you have recouped — so they share one screen and one service. The only
 * thing that differs is what you call it, and all of that lives here rather than as
 * ternaries scattered through two pages.
 */
export interface Wording {
  /** Heading for the group in the list. */
  group: string
  /** What you put out. */
  out: string
  /** What has returned. */
  back: string
  /** What has not returned yet. */
  remaining: string
  /** Anything past the capital. */
  surplus: string
  /** Fully recouped. */
  settled: string
  /** Label for the name field. */
  nameLabel: string
  namePlaceholder: string
  /** Heading above the outgoing head picker. */
  outHeads: string
  outHeadsHint: string
  /** Heading above the incoming head picker. */
  outHeadsEmpty: string
  backHeads: string
  backHeadsHint: string
  backHeadsEmpty: string
  /** Column-chart headings. */
  outEachCycle: string
  backEachCycle: string
  /** Row captions in the merged transaction list. */
  outEntry: string
  backEntry: string
  /** Sentence under the ring once everything is back. */
  recouped: (surplus: string) => string
  progress: (percent: number, started: string) => string
  nothingYet: (started: string) => string
}

const investment: Wording = {
  group: 'Investments',
  out: 'Put in',
  back: 'Come back',
  remaining: 'Still out',
  surplus: 'Gain',
  settled: 'Recouped',
  nameLabel: 'What is it?',
  namePlaceholder: 'Shares, a shop, a plot of land',
  outHeads: 'Where the money goes in',
  outHeadsHint:
    'Spending heads. Every expense on these counts as money invested, so don’t use them for anything else.',
  outHeadsEmpty: 'Nothing linked yet — no money will be counted as invested.',
  backHeads: 'Where the returns come back',
  backHeadsHint: 'Income heads. Anything you log on these counts as money coming back out of it.',
  backHeadsEmpty: 'Nothing linked yet — returns won’t be counted until you link one.',
  outEachCycle: 'Put in each cycle',
  backEachCycle: 'Came back each cycle',
  outEntry: 'put in',
  backEntry: 'came back',
  recouped: (surplus) => `You have your money back, and ${surplus} on top.`,
  progress: (percent, started) => `${percent}% of it has come back · started ${started}`,
  nothingYet: (started) => `Nothing put in yet · started ${started}`,
}

const lend: Wording = {
  group: 'Lent out',
  out: 'Lent',
  back: 'Paid back',
  remaining: 'Still owed',
  surplus: 'Extra',
  settled: 'Settled',
  nameLabel: 'What was it for?',
  namePlaceholder: 'Rent bond, a car, a favour',
  outHeads: 'Where the money went out',
  outHeadsHint:
    'Spending heads. Every expense on these counts as money lent, so don’t use them for anything else.',
  outHeadsEmpty: 'Nothing linked yet — nothing will be counted as lent.',
  backHeads: 'Where repayments arrive',
  backHeadsHint: 'Income heads. Anything you log on these counts as money paid back to you.',
  backHeadsEmpty: 'Nothing linked yet — repayments won’t be counted until you link one.',
  outEachCycle: 'Lent each cycle',
  backEachCycle: 'Paid back each cycle',
  outEntry: 'lent',
  backEntry: 'paid back',
  recouped: (surplus) => `All paid back, and ${surplus} more than you lent.`,
  progress: (percent, started) => `${percent}% paid back · lent from ${started}`,
  nothingYet: (started) => `Nothing lent yet · from ${started}`,
}

export function wordingFor(kind: InvestmentKind): Wording {
  return kind === 'Lend' ? lend : investment
}
