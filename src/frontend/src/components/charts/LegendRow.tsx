/** One line of a chart's legend: a swatch, a label, and the figure it stands for. */
export default function LegendRow({
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
