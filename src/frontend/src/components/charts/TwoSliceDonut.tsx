import type { ReactNode } from 'react'
import { useMoney } from '../../lib/money'

export interface Slice {
  label: string
  value: number
  color: string
}

/**
 * A thin two-slice ring. By default the centre is left open and the figures live in
 * the legend beside it — they collided with the arcs when they were crammed into the
 * hole. Pass `centre` where there is one figure worth putting inside, and give it a
 * larger `size` so it has room.
 */
export default function TwoSliceDonut({
  first,
  second,
  size = 108,
  centre,
}: {
  first: Slice
  second: Slice
  size?: number
  centre?: ReactNode
}) {
  const { format } = useMoney()

  const stroke = 10
  const radius = size / 2 - stroke / 2 - 3
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

  const ring = (
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

  if (!centre) return ring

  return (
    <span className="relative inline-grid shrink-0 place-items-center">
      {ring}
      <span className="absolute inset-0 grid place-items-center px-5 text-center">{centre}</span>
    </span>
  )
}
