import { apiClient } from './client'

export interface Expense {
  id: string
  headId: string
  headName: string
  categoryId: string
  categoryName: string
  amount: number
  expenseDate: string
  note: string | null
}

export interface ExpenseList {
  items: Expense[]
  totalCount: number
  totalAmount: number
  page: number
  pageSize: number
}

export interface SaveExpense {
  headId: string
  amount: number
  expenseDate: string
  note?: string
}

export interface ExpenseFilters {
  from?: string
  to?: string
  categoryId?: string
  headId?: string
  page?: number
}

export const expensesApi = {
  list: (filters: ExpenseFilters = {}) => {
    const params = new URLSearchParams()
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== '') params.set(key, String(value))
    })
    return apiClient.get<ExpenseList>(`/api/expenses?${params}`)
  },
  create: (data: SaveExpense) => apiClient.post<Expense>('/api/expenses', data),
  update: (id: string, data: SaveExpense) => apiClient.put<Expense>(`/api/expenses/${id}`, data),
  remove: (id: string) => apiClient.delete<void>(`/api/expenses/${id}`),
}
