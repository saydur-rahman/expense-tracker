import { authApiClient } from './client'
import { userManager, AUTH_BASE_URL } from '../auth/oidc'

export interface AdminUser {
  id: string
  email: string
  displayName: string
  roles: string[]
  isActive: boolean
  lastLoginAtUtc: string | null
  deactivatedAtUtc: string | null
  createdAtUtc: string
}

export interface AdminUserList {
  items: AdminUser[]
  totalCount: number
  page: number
  pageSize: number
}

export const adminApi = {
  listUsers: (search: string, page = 1) => {
    const params = new URLSearchParams({ page: String(page) })
    if (search) params.set('search', search)
    return authApiClient.get<AdminUserList>(`/api/admin/users?${params}`)
  },
  deactivate: (id: string) => authApiClient.post<AdminUser>(`/api/admin/users/${id}/deactivate`),
  reactivate: (id: string) => authApiClient.post<AdminUser>(`/api/admin/users/${id}/reactivate`),

  /**
   * RFC 8693 token exchange: trades the admin's access token for a read-only one
   * acting as the target user. Goes straight to Auth019's token endpoint because
   * it is an OAuth flow, not a REST call.
   */
  impersonate: async (userId: string): Promise<string> => {
    const admin = await userManager.getUser()
    if (!admin?.access_token) {
      throw new Error('You are not signed in.')
    }

    const body = new URLSearchParams({
      grant_type: 'urn:ietf:params:oauth:grant-type:token-exchange',
      client_id: 'expensetracker019-spa',
      subject_token: admin.access_token,
      subject_token_type: 'urn:ietf:params:oauth:token-type:access_token',
      requested_subject: userId,
    })

    const response = await fetch(`${AUTH_BASE_URL}/connect/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body,
    })

    const payload = await response.json().catch(() => null)

    if (!response.ok) {
      throw new Error(payload?.error_description ?? 'Could not start impersonation.')
    }

    return payload.access_token as string
  },
}
