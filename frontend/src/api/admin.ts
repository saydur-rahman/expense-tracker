import { apiClient } from './client'

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

export interface ImpersonationResponse {
  accessToken: string
  expiresAtUtc: string
  target: AdminUser
}

export const adminApi = {
  listUsers: (search: string, page = 1) => {
    const params = new URLSearchParams({ page: String(page) })
    if (search) params.set('search', search)
    return apiClient.get<AdminUserList>(`/api/admin/users?${params}`)
  },
  deactivate: (id: string) => apiClient.post<AdminUser>(`/api/admin/users/${id}/deactivate`),
  reactivate: (id: string) => apiClient.post<AdminUser>(`/api/admin/users/${id}/reactivate`),
  impersonate: (id: string) => apiClient.post<ImpersonationResponse>(`/api/admin/users/${id}/impersonate`),
}
