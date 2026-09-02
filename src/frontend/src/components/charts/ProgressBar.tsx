/**
 * A bare fraction bar: how much of `total` the `value` covers. Nothing to draw when
 * there is no total to measure against, so it renders nothing rather than an empty
 * track that would read as "zero".
 */
export default function ProgressBar({
  value,
  total,
  thin,
  fill,
  overFill = 'bg-negative-500',
}: {
  value: number
  total: number | null
  thin?: boolean
  /** Tailwind background class while within the total. */
  fill?: string
  /** Used instead once `value` passes `total`. */
  overFill?: string
}) {
  if (total === null || total === 0) {
    return null
  }

  const pct = Math.min(100, (value / total) * 100)
  const over = value > total

  return (
    <div className={`mt-1.5 overflow-hidden rounded-full bg-track ${thin ? 'h-1' : 'h-2'}`}>
      <div
        className={`h-full transition-all ${over ? overFill : (fill ?? 'bg-brand-500')}`}
        style={{ width: `${pct}%` }}
      />
    </div>
  )
}
