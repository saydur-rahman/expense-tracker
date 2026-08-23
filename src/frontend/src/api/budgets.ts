import { apiClient } from './client'

export interface HeadBudget {
  headId: string
  headName: string
  amount: number | null
}

export interface CategoryBudget {
  categoryId: string
  categoryName: string
  amount: number | null
  allocatedToHeads: number
  unallocated: number | null
  heads: HeadBudget[]
}

export interface PeriodBudgets {
  periodId: string
  periodLabel: string
  startDate: string
  endDate: string
  categories: CategoryBudget[]
}

export const budgetsApi = {
  get: (periodId: string) => apiClient.get<PeriodBudgets>(`/api/budget-periods/${periodId}/budgets`),
  setCategory: (periodId: string, categoryId: string, amount: number) =>
    apiClient.put<PeriodBudgets>(`/api/budget-periods/${periodId}/categories/${categoryId}/budget`, { amount }),
  clearCategory: (periodId: string, categoryId: string) =>
    apiClient.delete<PeriodBudgets>(`/api/budget-periods/${periodId}/categories/${categoryId}/budget`),
  setHead: (periodId: string, headId: string, amount: number) =>
    apiClient.put<PeriodBudgets>(`/api/budget-periods/${periodId}/heads/${headId}/budget`, { amount }),
  clearHead: (periodId: string, headId: string) =>
    apiClient.delete<PeriodBudgets>(`/api/budget-periods/${periodId}/heads/${headId}/budget`),
}
