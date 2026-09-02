import { useQuery } from '@tanstack/react-query'
import type { Category } from '../api/categories'
import { expensesApi } from '../api/expenses'
import { incomesApi } from '../api/incomes'
import { useMoney } from '../lib/money'

/**
 * What linking these heads is about to sweep in.
 *
 * Every row on a linked head counts, back to the loan's or investment's own date — which
 * is right when the head has always been for this one, and wrong when it was used for
 * ordinary spending in between. The behaviour is worth keeping either way; the surprise
 * is not. So it is shown before you save, while the date and the links are still in reach.
 *
 * No new endpoint: the expenses and incomes lists already return a server-computed
 * `totalCount` and `totalAmount` for any head-and-date filter, so asking for a single row
 * gets the whole answer.
 */
export default function LinkedHeadWarning({
  headIds,
  from,
  ledger,
  categories,
  counts,
}: {
  headIds: string[]
  /** The loan's or investment's own date — nothing before it is counted anyway. */
  from: string
  ledger: 'Expense' | 'Income'
  /** Used to name each head. */
  categories: Category[]
  /** How the rows will be treated, e.g. "count as repayments". */
  counts: string
}) {
  const { format } = useMoney()

  const { data } = useQuery({
    // Sorted so re-ordering the chips doesn't re-fetch.
    queryKey: ['linked-head-activity', ledger, from, [...headIds].sort()],
    enabled: headIds.length > 0 && from !== '',
    queryFn: async () => {
      const list = ledger === 'Income' ? incomesApi.list : expensesApi.list
      return Promise.all(
        headIds.map(async (headId) => {
          // pageSize 1: the totals describe the whole filter, not the page.
          const page = await list({ from, headId, pageSize: 1 })
          return { headId, count: page.totalCount, total: page.totalAmount }
        }),
      )
    },
  })

  const withRows = data?.filter((h) => h.count > 0) ?? []
  if (withRows.length === 0) return null

  const nameOf = (headId: string) => {
    for (const category of categories) {
      const head = category.heads.find((h) => h.id === headId)
      if (head) return `${category.name} › ${head.name}`
    }
    return 'that head'
  }

  const noun = ledger === 'Income' ? 'entries' : 'expenses'
  const one = ledger === 'Income' ? 'entry' : 'expense'

  return (
    <div className="rounded-lg bg-surplus-100 p-3 text-xs text-surplus-800 dark:bg-surplus-950 dark:text-surplus-300">
      <p className="font-medium">
        <span aria-hidden="true">⚠ </span>
        Already has history that will {counts}
      </p>
      <ul className="mt-1.5 flex flex-col gap-0.5">
        {withRows.map((head) => (
          <li key={head.headId}>
            <strong className="font-medium">{nameOf(head.headId)}</strong> — {head.count}{' '}
            {head.count === 1 ? one : noun} totalling {format(head.total)} since {from}
          </li>
        ))}
      </ul>
      <p className="mt-1.5 opacity-90">
        That is right if the head has always been for this. If not, change the date above or
        unlink the head.
      </p>
    </div>
  )
}
