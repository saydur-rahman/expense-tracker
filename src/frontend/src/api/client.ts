import { userManager, getImpersonationToken } from '../auth/oidc'
import { userTimeZone } from '../lib/dates'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL as string

export class ApiError extends Error {
  status: number
  body: unknown

  constructor(status: number, message: string, body: unknown) {
    super(message)
    this.status = status
    this.body = body
  }
}

/**
 * The token to present to the API. While impersonating, that is the read-only
 * exchange token rather than the admin's own — which is exactly why writes fail
 * with 403 during impersonation.
 */
async function getAccessToken(): Promise<string | null> {
  const impersonationToken = getImpersonationToken()
  if (impersonationToken) return impersonationToken

  const user = await userManager.getUser()
  return user?.access_token ?? null
}

async function request<T>(baseUrl: string, path: string, init?: RequestInit): Promise<T> {
  const token = await getAccessToken()

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      // Sent on every request so the server can work out the caller's "today". Doing it
      // here rather than per-endpoint means a new endpoint cannot forget to ask.
      'X-Time-Zone': userTimeZone(),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  })

  if (response.status === 401) {
    // The session is gone or expired; start a fresh sign-in.
    await userManager.signinRedirect()
    throw new ApiError(401, 'Your session has expired.', null)
  }

  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new ApiError(response.status, body?.title ?? response.statusText, body)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const apiClient = {
  get: <T>(path: string) => request<T>(API_BASE_URL, path),
  post: <T>(path: string, data?: unknown) =>
    request<T>(API_BASE_URL, path, { method: 'POST', body: data ? JSON.stringify(data) : undefined }),
  put: <T>(path: string, data?: unknown) =>
    request<T>(API_BASE_URL, path, { method: 'PUT', body: data ? JSON.stringify(data) : undefined }),
  delete: <T>(path: string) => request<T>(API_BASE_URL, path, { method: 'DELETE' }),
}

/** Auth019 hosts the user-administration API, so admin calls go to a different origin. */
export const authApiClient = {
  get: <T>(path: string) => request<T>(import.meta.env.VITE_AUTH_BASE_URL as string, path),
  post: <T>(path: string, data?: unknown) =>
    request<T>(import.meta.env.VITE_AUTH_BASE_URL as string, path, {
      method: 'POST',
      body: data ? JSON.stringify(data) : undefined,
    }),
  put: <T>(path: string, data?: unknown) =>
    request<T>(import.meta.env.VITE_AUTH_BASE_URL as string, path, {
      method: 'PUT',
      body: data ? JSON.stringify(data) : undefined,
    }),
}
