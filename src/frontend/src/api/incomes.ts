import { apiClient } from './client'

export interface Income {
  id: string
  headId: string
  headName: string
  categoryId: string
  categoryName: string
  amount: number
  incomeDate: string
  note: string | null
}

export interface IncomeList {
  items: Income[]
  totalCount: number
  totalAmount: number
  page: number
  pageSize: number
}

export interface SaveIncome {
  headId: string
  amount: number
  incomeDate: string
  note?: string
}

export interface IncomeFilters {
  from?: string
  to?: string
  categoryId?: string
  headId?: string
  page?: number
  pageSize?: number
}

export const incomesApi = {
  list: (filters: IncomeFilters = {}) => {
    const params = new URLSearchParams()
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== '') params.set(key, String(value))
    })
    return apiClient.get<IncomeList>(`/api/incomes?${params}`)
  },
  create: (data: SaveIncome) => apiClient.post<Income>('/api/incomes', data),
  update: (id: string, data: SaveIncome) => apiClient.put<Income>(`/api/incomes/${id}`, data),
  remove: (id: string) => apiClient.delete<void>(`/api/incomes/${id}`),
}
