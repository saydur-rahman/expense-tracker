/**
 * Dates as the user sees them, not as the server stores them.
 *
 * `new Date().toISOString()` converts to UTC first, so in New Zealand (UTC+12/+13) it
 * hands back *yesterday* for the whole working day — every form opened pre-filled with
 * the wrong day and had to be corrected by hand. These read the browser's local calendar
 * instead.
 */

/** Today where the user is, as `YYYY-MM-DD` — the format every date input wants. */
export function todayLocal(): string {
  return toLocalDateString(new Date())
}

/** A `Date` as its local `YYYY-MM-DD`, with no timezone conversion. */
export function toLocalDateString(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/**
 * The browser's IANA zone, e.g. `Pacific/Auckland`. Sent on every API request so the
 * server can work out the caller's "today" rather than its own.
 *
 * Falls back to UTC: an environment without `Intl` should lose the improvement, not the
 * ability to make a request.
 */
export function userTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch {
    return 'UTC'
  }
}
