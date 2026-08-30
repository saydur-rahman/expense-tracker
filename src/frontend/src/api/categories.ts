import { apiClient } from './client'

/** Which ledger a category belongs to. Income shares the structure but takes no budget. */
export type CategoryKind = 'Expense' | 'Income'

export interface Head {
  id: string
  categoryId: string
  name: string
  isArchived: boolean
}

export interface Category {
  id: string
  name: string
  kind: CategoryKind
  isArchived: boolean
  heads: Head[]
}

export const categoriesApi = {
  list: (kind: CategoryKind = 'Expense', includeArchived = false) =>
    apiClient.get<Category[]>(`/api/categories?kind=${kind}&includeArchived=${includeArchived}`),
  create: (name: string, kind: CategoryKind = 'Expense') =>
    apiClient.post<Category>('/api/categories', { name, kind }),
  rename: (id: string, name: string) => apiClient.put<Category>(`/api/categories/${id}`, { name }),
  archive: (id: string) => apiClient.delete<void>(`/api/categories/${id}`),
  createHead: (categoryId: string, name: string) =>
    apiClient.post<Head>(`/api/categories/${categoryId}/heads`, { name }),
  renameHead: (id: string, name: string) => apiClient.put<Head>(`/api/heads/${id}`, { name }),
  archiveHead: (id: string) => apiClient.delete<void>(`/api/heads/${id}`),
}
