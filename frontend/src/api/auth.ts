import { apiClient } from './client'

export interface UserDto {
  id: string
  email: string
  displayName: string
  roles: string[]
  isImpersonating: boolean
  impersonatedBy: string | null
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAtUtc: string
  user: UserDto
}

export const authApi = {
  register: (data: { email: string; password: string; displayName: string }) =>
    apiClient.post<AuthResponse>('/api/auth/register', data),
  login: (data: { email: string; password: string }) =>
    apiClient.post<AuthResponse>('/api/auth/login', data),
  google: (idToken: string) => apiClient.post<AuthResponse>('/api/auth/google', { idToken }),
  refresh: (refreshToken: string) =>
    apiClient.post<AuthResponse>('/api/auth/refresh', { refreshToken }),
  me: () => apiClient.get<UserDto>('/api/auth/me'),
}
