import { apiClient } from './client'

/** The rhythm a user budgets in. One at a time — chosen in Settings. */
export type PeriodKind = 'Month' | 'Week'

/** Matches .NET's DayOfWeek names, which the API serialises as strings. */
export type WeekDay =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'

export const WEEK_DAYS: WeekDay[] = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
]

export interface MonthCycle {
  periodKind: PeriodKind
  /** Day of the month. Only governs the cycle when periodKind is 'Month'. */
  startDay: number
  /** Only governs the cycle when periodKind is 'Week'. */
  weekStartsOn: WeekDay
  isConfigured: boolean
}

/**
 * Both fields go up whichever rhythm is chosen, so the unused one is preserved rather
 * than reset — switching to weekly and back keeps the day of the month you had picked.
 */
export interface SaveMonthCycle {
  periodKind: PeriodKind
  startDay: number
  weekStartsOn: WeekDay
}

export interface BudgetPeriod {
  id: string
  kind: PeriodKind
  startDate: string
  endDate: string
  label: string
}

export const settingsApi = {
  getMonthCycle: () => apiClient.get<MonthCycle>('/api/settings/month-cycle'),
  updateMonthCycle: (data: SaveMonthCycle) =>
    apiClient.put<MonthCycle>('/api/settings/month-cycle', data),
}

export const budgetPeriodsApi = {
  current: () => apiClient.get<BudgetPeriod>('/api/budget-periods/current'),
  relative: (offset: number) => apiClient.get<BudgetPeriod>(`/api/budget-periods/relative/${offset}`),
  list: () => apiClient.get<BudgetPeriod[]>('/api/budget-periods'),
}
