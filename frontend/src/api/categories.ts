import { apiClient } from './client'

export interface Head {
  id: string
  categoryId: string
  name: string
  isArchived: boolean
}

export interface Category {
  id: string
  name: string
  isArchived: boolean
  heads: Head[]
}

export const categoriesApi = {
  list: (includeArchived = false) =>
    apiClient.get<Category[]>(`/api/categories?includeArchived=${includeArchived}`),
  create: (name: string) => apiClient.post<Category>('/api/categories', { name }),
  rename: (id: string, name: string) => apiClient.put<Category>(`/api/categories/${id}`, { name }),
  archive: (id: string) => apiClient.delete<void>(`/api/categories/${id}`),
  createHead: (categoryId: string, name: string) =>
    apiClient.post<Head>(`/api/categories/${categoryId}/heads`, { name }),
  renameHead: (id: string, name: string) => apiClient.put<Head>(`/api/heads/${id}`, { name }),
  archiveHead: (id: string) => apiClient.delete<void>(`/api/heads/${id}`),
}
