import type { InputHTMLAttributes } from 'react'
import { readAmount } from '../lib/calc'
import { useMoney } from '../lib/money'

/**
 * What the typed text works out to, shown under an amount input while it is arithmetic.
 * Silent for a plain number — there is nothing to tell someone who typed 20 that they
 * don't already know.
 *
 * Exported on its own so a field with its own layout (the compact ones on the Budgets
 * screen) shows the same line without duplicating the wording.
 */
export function AmountHint({ raw, className = '' }: { raw: string; className?: string }) {
  const money = useMoney()
  const reading = readAmount(raw)

  if (reading.kind === 'expression') {
    return (
      <span className={`block text-xs tabular-nums text-ink-muted ${className}`}>
        = {money.format(reading.value)}
      </span>
    )
  }

  if (reading.kind === 'invalid') {
    return (
      <span className={`block text-xs text-negative-600 dark:text-negative-400 ${className}`}>
        That isn't an amount we can work out.
      </span>
    )
  }

  return null
}

interface AmountFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange'> {
  value: string
  onChange: (raw: string) => void
  /** Applied to the wrapper, so the field can still be flexed by its parent. */
  wrapperClassName?: string
}

/**
 * An amount input that accepts arithmetic: type `635*3` and it submits 1905.
 *
 * `inputMode` defaults to text rather than decimal. A decimal keypad on iOS offers digits
 * and a separator only, with no way to reach `*` or `/` — which would leave the arithmetic
 * working on desktop and not on the phone this app is built for. Pass `inputMode="decimal"`
 * on a field where the numeric keypad matters more.
 */
export default function AmountField({
  value,
  onChange,
  wrapperClassName = '',
  className = '',
  inputMode = 'text',
  ...props
}: AmountFieldProps) {
  return (
    <div className={wrapperClassName}>
      <input
        inputMode={inputMode}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className={className}
        {...props}
      />
      <AmountHint raw={value} className="mt-1" />
    </div>
  )
}
