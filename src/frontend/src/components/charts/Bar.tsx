import type { ReactNode } from 'react'
import { eyebrow } from '../ui'

/**
 * A labelled bar with its figure underneath, and an optional tick marking where some
 * reference value falls on the same scale.
 *
 * Several of these drawn to one shared scale is how a set of amounts is compared in
 * this app — see `PeriodOverview`, where the tick is the budget and every other bar
 * is read against it.
 */
export default function Bar({
  label,
  value,
  caption,
  width,
  fill,
  mark,
  note,
}: {
  label: string
  value: string
  caption?: string
  /** Percentage of the shared scale. */
  width: number
  /** Tailwind background class for the fill. */
  fill: string
  /** Where the reference value sits on the same scale, as a percentage. Null to omit. */
  mark?: number | null
  note?: ReactNode
}) {
  return (
    <div>
      <div className="relative h-2.5 overflow-hidden rounded-full bg-track">
        <div
          className={`h-full rounded-full transition-all ${fill}`}
          style={{ width: `${width}%` }}
        />
        {mark != null && (
          // Drawn over the fill, so a bar that beats the reference is seen crossing it.
          <span
            aria-hidden="true"
            className="absolute inset-y-0 w-0.5 bg-ink/45"
            style={{ left: `calc(${mark}% - 1px)` }}
          />
        )}
      </div>

      <div className="mt-1 flex items-baseline justify-between gap-2">
        <span className={eyebrow}>{label}</span>
        <span className="tabular-nums text-sm font-semibold text-ink">{value}</span>
      </div>

      {caption && <p className="text-[11px] leading-tight text-ink-muted">{caption}</p>}
      {note}
    </div>
  )
}
