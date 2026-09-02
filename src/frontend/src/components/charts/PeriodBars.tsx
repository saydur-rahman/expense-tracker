import { useMoney } from '../../lib/money'

export interface PeriodBucket {
  label: string
  amount: number
}

/**
 * One column per cycle, oldest on the left — what went in or out over the last
 * several months or weeks.
 *
 * Hand-rolled SVG on purpose: the app carries no charting library and every other
 * visual here is inline SVG or a div with a percentage width. Adding one for a single
 * column chart would be 40x the bundle for less control.
 */
export default function PeriodBars({
  buckets,
  color,
  emptyLabel = 'Nothing yet.',
}: {
  buckets: PeriodBucket[]
  color: string
  emptyLabel?: string
}) {
  const { format } = useMoney()

  const max = Math.max(0, ...buckets.map((b) => b.amount))

  if (buckets.length === 0 || max === 0) {
    return <p className="text-xs text-ink-muted">{emptyLabel}</p>
  }

  return (
    <div
      role="img"
      aria-label={buckets.map((b) => `${b.label} ${format(b.amount)}`).join(', ')}
      className="flex items-end gap-1.5 overflow-x-auto"
    >
      {buckets.map((bucket) => {
        // A cycle with nothing in it still gets a hairline, so the gap in the run is
        // visible as a gap rather than as a missing column.
        const height = bucket.amount > 0 ? Math.max(4, (bucket.amount / max) * 72) : 2

        return (
          <div key={bucket.label} className="flex min-w-8 flex-1 flex-col items-center gap-1">
            <div className="flex h-[72px] w-full items-end">
              <div
                className="w-full rounded-t transition-all"
                style={{
                  height: `${height}px`,
                  backgroundColor: bucket.amount > 0 ? color : undefined,
                }}
                title={`${bucket.label} · ${format(bucket.amount)}`}
              >
                {bucket.amount === 0 && <div className="h-full w-full rounded-t bg-track" />}
              </div>
            </div>
            <span className="truncate text-[10px] leading-tight text-ink-muted">
              {bucket.label}
            </span>
          </div>
        )
      })}
    </div>
  )
}
