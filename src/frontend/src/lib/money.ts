import { useMemo } from 'react'
import { useAuth } from '../auth/AuthContext'

/**
 * Formats amounts in the signed-in user's currency, which Auth019 derives from
 * the country on their profile and puts on the token.
 *
 * Accounts with no country — everyone who registered before that was collected —
 * fall back to plain grouped numbers rather than guessing a currency and showing
 * someone the wrong symbol.
 */
function buildFormatter(currency: string | null) {
  if (currency) {
    try {
      return new Intl.NumberFormat(undefined, {
        style: 'currency',
        currency,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      })
    } catch {
      // An unrecognised currency code would otherwise throw on every render.
    }
  }

  return new Intl.NumberFormat(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}

export function useMoney() {
  const { user } = useAuth()
  const currency = user?.currency ?? null

  return useMemo(() => {
    const formatter = buildFormatter(currency)
    return {
      currency,
      /** e.g. "৳4,500.00" — or "4,500.00" when the account has no country yet. */
      format: (value: number) => formatter.format(value),
    }
  }, [currency])
}
