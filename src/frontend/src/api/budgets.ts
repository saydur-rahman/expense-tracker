import { apiClient } from './client'

export interface HeadBudget {
  headId: string
  headName: string
  amount: number | null
}

export interface CategoryBudget {
  categoryId: string
  categoryName: string
  /** The budget in force: the head total once any head is budgeted, otherwise the target. */
  amount: number | null
  /** Optional figure on the category itself. A target to aim at, never a cap. */
  target: number | null
  allocatedToHeads: number
  /** Head total minus target: positive is extra, negative is short. Null if not comparable. */
  difference: number | null
  heads: HeadBudget[]
}

export interface PeriodBudgets {
  periodId: string
  periodLabel: string
  startDate: string
  endDate: string
  /** Income logged in this period — what there is to divide up. */
  totalIncome: number
  /** Every category's budget in force, added together. */
  totalBudgeted: number
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
