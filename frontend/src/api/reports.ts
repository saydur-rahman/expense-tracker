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
  categories: CategorySummary[]
}

export const reportsApi = {
  summary: (periodId: string) => apiClient.get<PeriodSummary>(`/api/reports/summary?periodId=${periodId}`),
  currentSummary: () => apiClient.get<PeriodSummary>('/api/reports/summary/current'),
}
