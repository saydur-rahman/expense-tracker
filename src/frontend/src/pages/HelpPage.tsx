import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { card, pageTitle } from '../components/ui'

/**
 * What the app is and how to use it, in the order someone actually meets it.
 *
 * This page is the app's user-facing documentation — **keep it in step with the app**.
 * Any change that alters what a user sees or does belongs here too; see the "Help page"
 * convention in AGENTS.md.
 */
export default function HelpPage() {
  const { isAdmin } = useAuth()

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className={pageTitle}>Help</h1>
        <p className="mt-2 text-sm text-ink-muted">
          What this app does and how to get the most out of it.
        </p>
      </div>

      <section className={`${card} p-4`}>
        <h2 className="font-medium text-ink">In short</h2>
        <p className="mt-2 text-sm leading-relaxed text-ink-soft">
          Expense Tracker answers one question:{' '}
          <strong className="font-medium text-ink">how much of this month is left?</strong> You
          set a budget for each kind of spending, log what you spend as you go, and the
          dashboard shows what remains. Income is tracked alongside it, on its own ledger,
          so you can also see what you managed to save.
        </p>
        <p className="mt-2 text-sm leading-relaxed text-ink-soft">
          If you budget week to week rather than month to month, say so in step 1 and every
          screen below reads the same way — just over a week.
        </p>
        <p className="mt-2 text-sm leading-relaxed text-ink-soft">
          It is built for a phone. Everything works one-handed, and the tabs along the
          bottom are the whole app.
        </p>
      </section>

      <Step
        n={1}
        title="Choose your budget cycle"
        where={<Link className="underline hover:text-ink" to="/settings/month-cycle">Settings → Budget cycle</Link>}
      >
        <p>
          First pick the rhythm you budget in —{' '}
          <strong className="font-medium text-ink">monthly</strong> or{' '}
          <strong className="font-medium text-ink">weekly</strong>. Every total, budget and
          report is cut to whichever you choose.
        </p>
        <p>
          <strong className="font-medium text-ink">Monthly</strong> doesn't have to run 1st to
          31st — most budgeting runs payday to payday. Pick the day yours begins: 1 for a
          normal calendar month, or your salary date (say 25) to track salary-to-salary. In a
          short month a start day past the 28th falls back to the last day available.
        </p>
        <p>
          <strong className="font-medium text-ink">Weekly</strong> asks which day your week
          begins instead, and gives you a fresh seven-day budget every one of those days.
        </p>
        <p>
          Change your mind later and the current period is re-cut immediately — you don't have
          to wait for the next one. Switching between monthly and weekly leaves everything you
          have already budgeted untouched, and your first period on the new rhythm starts empty:
          a monthly figure sliced into weeks would be an amount you never chose.
        </p>
      </Step>

      <Step
        n={2}
        title="Build your categories and heads"
        where={<Link className="underline hover:text-ink" to="/categories">Categories</Link>}
      >
        <p>
          Spending is organised two levels deep. A{' '}
          <strong className="font-medium text-ink">category</strong> is the broad group —
          Food, Transport, Home. A <strong className="font-medium text-ink">head</strong> is
          the specific thing inside it — Groceries, Eating out, Fuel.
        </p>
        <p>
          Type a name and press <em>Add</em> to create a category, then add heads under it.
          Tap any name to rename it. Two levels is the whole hierarchy; there is no third.
        </p>
        <p>
          The <strong className="font-medium text-ink">Expense / Income</strong> switch at
          the top gives you a second, separate tree for money coming in — Salary, Freelance,
          Gifts. The two never mix: an income head takes no budget, and you can't log
          spending against it.
        </p>
      </Step>

      <Step
        n={3}
        title="Give each one a budget"
        where={<Link className="underline hover:text-ink" to="/budgets">Budgets</Link>}
      >
        <p>
          Put a figure on each <strong className="font-medium text-ink">head</strong> — that
          is all you have to do. The category is simply what its heads add up to, so there is
          no need to budget the category first, or at all.
        </p>
        <p>
          Each category also has an optional <strong className="font-medium text-ink">target</strong>:
          what you meant to spend there. It doesn't limit anything. If your heads add up to
          more or less than it, the card tells you by how much — "200 extra" or "150 short" —
          and leaves the decision to you. Set a target on its own, with no head figures, and
          it simply becomes the category's budget.
        </p>
        <p>
          Across the top the screen shows your{' '}
          <strong className="font-medium text-ink">income for the same period</strong>, what
          you have budgeted so far, and what is{' '}
          <strong className="font-medium text-ink">left to budget</strong> — so you are
          dividing up a real number rather than working from memory. Budget past your income
          and it turns red and counts down below zero. Nothing stops you; it just says so.
        </p>
        <p>
          Tap a category to fold it away. With more than a handful they start folded, so the
          whole period fits on one screen.
        </p>
        <p>
          <strong className="font-medium text-ink">You only need to do this once.</strong> A
          new month inherits the previous month's figures, so a budget you set in July still
          applies in August. Edit a month and only that month changes — the ones after it
          inherit the edit, the ones before are left alone. A month you deliberately empty
          stays empty. Weeks behave identically: each new week inherits the last one's figures.
        </p>
        <p className="text-ink-muted">Income takes no budget. That is deliberate, not missing.</p>
      </Step>

      <Step
        n={4}
        title="Log as you spend"
        where={
          <>
            <Link className="underline hover:text-ink" to="/expenses">Expenses</Link>
            {' and '}
            <Link className="underline hover:text-ink" to="/incomes">Income</Link>
          </>
        }
      >
        <p>
          Pick a head, type the amount, add a note if it helps you remember, and save. The
          note is optional — the amount and the head are what matter.
        </p>
        <p>
          <strong className="font-medium text-ink">Any amount box does sums.</strong> Type{' '}
          <code className="rounded bg-raised px-1 py-0.5 text-xs">635*3</code> and it saves
          1,905 — the running total appears under the box as you type. It handles{' '}
          <code className="rounded bg-raised px-1 py-0.5 text-xs">+ − × ÷</code> and brackets,
          so <code className="rounded bg-raised px-1 py-0.5 text-xs">(12+8)*4</code> works too.
          This is the same everywhere an amount is asked for, budgets included.
        </p>
        <p>
          Both screens list what you've logged in the current period and let you filter down
          to a single head. Income works the same way against your income heads.
        </p>
      </Step>

      <Step
        n={5}
        title="Watch the dashboard"
        where={<Link className="underline hover:text-ink" to="/">Dashboard</Link>}
      >
        <p>
          The bar at the top is your whole month — or week, if that's your rhythm: spent
          against budgeted. The donut beside
          it splits the same figure into{' '}
          <span className="font-medium text-positive-700 dark:text-positive-400">Left</span>{' '}
          and Spent. Go over and both turn{' '}
          <span className="font-medium text-negative-600 dark:text-negative-400">red</span>{' '}
          and the centre reads "Over".
        </p>
        <p>
          Below that, <strong className="font-medium text-ink">Income vs expense</strong>{' '}
          shows what came in against what went out, with the amount you saved called out
          underneath. Then a card per category shows how each is tracking. The{' '}
          <strong className="font-medium text-ink">Expense / Income</strong> tabs switch
          that breakdown between the two ledgers.
        </p>
        <p>
          Use the period picker to look back over earlier periods. Each keeps its own budgets
          and totals exactly as they were.
        </p>
      </Step>

      <section className={`${card} p-4`}>
        <h2 className="font-medium text-ink">Removing things keeps your history</h2>
        <div className="mt-2 flex flex-col gap-2 text-sm leading-relaxed text-ink-soft">
          <p>
            <em>Remove</em> on a category or head retires it: it disappears from the lists
            and from the pickers, so you can't log against it any more. But everything you
            already spent under it stays in your history and in every past month's figures.
          </p>
          <p>
            Nothing you have logged is ever silently deleted. That's the point — a report
            for last March should still read the way it did last March.
          </p>
        </div>
      </section>

      <section className={`${card} p-4`}>
        <h2 className="font-medium text-ink">Your account</h2>
        <div className="mt-2 flex flex-col gap-2 text-sm leading-relaxed text-ink-soft">
          <p>
            <strong className="font-medium text-ink">Currency</strong> comes from the country
            on your{' '}
            <Link className="underline hover:text-ink" to="/settings/profile">profile</Link>.
            Set the country and every amount in the app is shown in that currency. It updates
            as soon as you save.
          </p>
          <p>
            <strong className="font-medium text-ink">Signing in</strong> is either your email
            and password, or Google. Your email address is fixed — it's how the account is
            identified, so it can't be edited here.
          </p>
          <p>
            <strong className="font-medium text-ink">Changing your password</strong> is on the
            Profile screen: type the new one twice and save. If you signed up with Google
            there's no password card, because your password lives with Google, not here.
          </p>
          <p>
            <strong className="font-medium text-ink">Something to say?</strong>{' '}
            <Link className="underline hover:text-ink" to="/settings/feedback">Settings → Feedback</Link>{' '}
            sends it straight to us, and replies come back in the same thread.
          </p>
        </div>
      </section>

      {isAdmin && (
        <section className={`${card} p-4`}>
          <h2 className="font-medium text-ink">For admins</h2>
          <div className="mt-2 flex flex-col gap-2 text-sm leading-relaxed text-ink-soft">
            <p>
              <Link className="underline hover:text-ink" to="/admin/users">Admin → Users</Link>{' '}
              lists every account and lets you deactivate one, which immediately stops it
              signing in and cuts off any session it already had.
            </p>
            <p>
              <strong className="font-medium text-ink">Viewing as someone</strong> shows you
              their app exactly as they see it, to work out what went wrong. It is strictly
              read-only: every attempt to add, edit or remove anything is refused while you're
              in it, and a banner across the top says whose account you're in. Leave it from
              that banner.
            </p>
            <p>
              <Link className="underline hover:text-ink" to="/admin/feedback">Admin → Feedback</Link>{' '}
              is where user feedback arrives and gets answered.
            </p>
          </div>
        </section>
      )}

      <p className="pb-2 text-center text-xs text-ink-muted">
        Still stuck? Tell us on the{' '}
        <Link className="underline hover:text-ink-soft" to="/settings/feedback">Feedback</Link>{' '}
        screen — it's read by a person.
      </p>
    </div>
  )
}

/** A numbered walkthrough step, with the screen it refers to named up front. */
function Step({
  n,
  title,
  where,
  children,
}: {
  n: number
  title: string
  where: React.ReactNode
  children: React.ReactNode
}) {
  return (
    <section className={`${card} p-4`}>
      <div className="flex items-start gap-3">
        <span
          aria-hidden="true"
          className="mt-0.5 grid size-6 shrink-0 place-items-center rounded-full bg-brand-50 text-xs font-semibold text-brand-700 dark:bg-brand-950 dark:text-brand-300"
        >
          {n}
        </span>
        <div className="min-w-0">
          <h2 className="font-medium text-ink">{title}</h2>
          <p className="mt-0.5 text-xs text-ink-muted">{where}</p>
        </div>
      </div>
      <div className="mt-3 flex flex-col gap-2 text-sm leading-relaxed text-ink-soft">
        {children}
      </div>
    </section>
  )
}
