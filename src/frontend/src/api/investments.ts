import { apiClient } from './client'
import type { LinkedHead, PeriodTotal, TransactionFilters } from './loans'

export type InvestmentDirection = 'Contribution' | 'Return'

export interface Investment {
  id: string
  name: string
  remark: string | null
  startedOn: string
  /** Spending on the contribution heads, from `startedOn` onward. */
  invested: number
  /** Income on the return heads over the same window. */
  returned: number
  /** Invested minus returned, floored at zero — capital still out there. */
  outstanding: number
  percentReturned: number
  /** Returns beyond what you put in. The profit, once there is any. */
  gain: number
  isRecouped: boolean
  contributionHeads: LinkedHead[]
  returnHeads: LinkedHead[]
}

export interface InvestmentTransaction {
  id: string
  headId: string
  headName: string
  categoryName: string
  amount: number
  date: string
  note: string | null
  direction: InvestmentDirection
}

export interface InvestmentDetail {
  investment: Investment
  recentTransactions: InvestmentTransaction[]
  transactionCount: number
}

export interface InvestmentTransactionList {
  items: InvestmentTransaction[]
  totalCount: number
  totalInvested: number
  totalReturned: number
  page: number
  pageSize: number
}

export interface InvestmentVsIncome {
  periodLabel: string
  startDate: string
  endDate: string
  invested: number
  income: number
  /** Income not invested. Negative if you invested more than you earned. */
  remainder: number
  percentOfIncome: number
}

export interface SaveInvestmentRequest {
  name: string
  remark: string | null
  startedOn: string
  /** Both replace their linked set wholesale. */
  contributionHeadIds: string[]
  returnHeadIds: string[]
}

function query(filters: TransactionFilters) {
  const params = new URLSearchParams()
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== '') params.set(key, String(value))
  })
  return params.toString()
}

export const investmentsApi = {
  list: () => apiClient.get<Investment[]>('/api/investments'),
  get: (id: string) => apiClient.get<InvestmentDetail>(`/api/investments/${id}`),
  vsIncome: (periodId: string) =>
    apiClient.get<InvestmentVsIncome>(`/api/investments/vs-income?periodId=${periodId}`),
  transactions: (id: string, filters: TransactionFilters = {}) =>
    apiClient.get<InvestmentTransactionList>(`/api/investments/${id}/transactions?${query(filters)}`),
  byPeriod: (id: string, count = 12) =>
    apiClient.get<PeriodTotal[]>(`/api/investments/${id}/by-period?count=${count}`),
  create: (request: SaveInvestmentRequest) =>
    apiClient.post<Investment>('/api/investments', request),
  update: (id: string, request: SaveInvestmentRequest) =>
    apiClient.put<Investment>(`/api/investments/${id}`, request),
  remove: (id: string) => apiClient.delete<void>(`/api/investments/${id}`),
}
