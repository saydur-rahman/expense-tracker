/**
 * Reads the claims out of a JWT for display purposes only.
 *
 * This does NOT verify the signature and must never be used to make a security
 * decision — the API validates every token server-side. It exists so the UI can
 * show a name and hide admin links.
 */
export function decodeJwt(token: string): Record<string, unknown> | null {
  const payload = token.split('.')[1]
  if (!payload) return null

  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
    const json = decodeURIComponent(
      atob(padded)
        .split('')
        .map((c) => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join(''),
    )
    return JSON.parse(json) as Record<string, unknown>
  } catch {
    return null
  }
}
