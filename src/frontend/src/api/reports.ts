import { apiClient } from './client'

export interface HeadSummary {
  headId: string
  headName: string
  isArchived: boolean
  budget: number | null
  spent: number
  remaining: number | null
  isOverBudget: boolean
}

export interface CategorySummary {
  categoryId: string
  categoryName: string
  isArchived: boolean
  budget: number | null
  spent: number
  remaining: number | null
  isOverBudget: boolean
  heads: HeadSummary[]
}

export interface PeriodSummary {
  periodId: string
  periodLabel: string
  startDate: string
  endDate: string
  totalBudget: number
  totalSpent: number
  totalRemaining: number
  totalIncome: number
  /** Income minus spending. Negative means you spent more than you earned. */
  totalSaved: number
  /**
   * Spending the budget never accounted for — per category, whatever went past its
   * budget, with an unbudgeted category counting in full.
   */
  extraExpenses: number
  /**
   * A forecast: income − budget − extraExpenses. What the period ends with if the rest
   * of the budget is spent and nothing else is. `totalSaved` is where things stand now.
   */
  estimatedLeftOver: number
  categories: CategorySummary[]
  incomeCategories: CategorySummary[]
}

export const reportsApi = {
  summary: (periodId: string) => apiClient.get<PeriodSummary>(`/api/reports/summary?periodId=${periodId}`),
  currentSummary: () => apiClient.get<PeriodSummary>('/api/reports/summary/current'),
}
