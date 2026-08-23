import { apiClient } from './client'

export interface MonthCycle {
  startDay: number
  isConfigured: boolean
}

export interface BudgetPeriod {
  id: string
  startDate: string
  endDate: string
  label: string
}

export const settingsApi = {
  getMonthCycle: () => apiClient.get<MonthCycle>('/api/settings/month-cycle'),
  updateMonthCycle: (startDay: number) =>
    apiClient.put<MonthCycle>('/api/settings/month-cycle', { startDay }),
}

export const budgetPeriodsApi = {
  current: () => apiClient.get<BudgetPeriod>('/api/budget-periods/current'),
  relative: (offset: number) => apiClient.get<BudgetPeriod>(`/api/budget-periods/relative/${offset}`),
  list: () => apiClient.get<BudgetPeriod[]>('/api/budget-periods'),
}
