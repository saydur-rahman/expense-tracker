import { apiClient } from './client'

export interface LinkedHead {
  headId: string
  headName: string
  categoryId: string
  categoryName: string
  /** The head has since been removed. Its past rows still count. */
  isArchived: boolean
}

export interface Loan {
  id: string
  name: string
  lender: string | null
  amountTaken: number
  takenOn: string
  remark: string | null
  /** Spending on the linked heads, from `takenOn` onward. */
  repaid: number
  /** Taken minus repaid, floored at zero. */
  outstanding: number
  percentSettled: number
  /** Paid beyond what was taken — usually a payment against the wrong head. */
  overpaid: number
  isSettled: boolean
  heads: LinkedHead[]
}

export interface LoanTransaction {
  id: string
  headId: string
  headName: string
  categoryName: string
  amount: number
  date: string
  note: string | null
}

export interface LoanDetail {
  loan: Loan
  recentTransactions: LoanTransaction[]
  transactionCount: number
}

export interface LoanTransactionList {
  items: LoanTransaction[]
  totalCount: number
  totalAmount: number
  page: number
  pageSize: number
}

export interface PeriodTotal {
  label: string
  startDate: string
  endDate: string
  amount: number
  /** Only meaningful for investments, where a cycle has two sides. */
  secondaryAmount: number
}

export interface LoanPortfolio {
  periodLabel: string
  startDate: string
  endDate: string
  count: number
  settledCount: number
  borrowed: number
  repaid: number
  /** Still owed across every loan. */
  outstanding: number
  /** Paid in this cycle alone — what the period picker changes. */
  paidInPeriod: number
  percentSettled: number
}

export interface SaveLoanRequest {
  name: string
  lender: string | null
  amountTaken: number
  takenOn: string
  remark: string | null
  /** Replaces the linked set wholesale. */
  headIds: string[]
}

export interface TransactionFilters {
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

function query(filters: TransactionFilters) {
  const params = new URLSearchParams()
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== '') params.set(key, String(value))
  })
  return params.toString()
}

export const loansApi = {
  list: () => apiClient.get<Loan[]>('/api/loans'),
  portfolio: (periodId: string) =>
    apiClient.get<LoanPortfolio>(`/api/loans/portfolio?periodId=${periodId}`),
  get: (id: string) => apiClient.get<LoanDetail>(`/api/loans/${id}`),
  transactions: (id: string, filters: TransactionFilters = {}) =>
    apiClient.get<LoanTransactionList>(`/api/loans/${id}/transactions?${query(filters)}`),
  byPeriod: (id: string, count = 12) =>
    apiClient.get<PeriodTotal[]>(`/api/loans/${id}/by-period?count=${count}`),
  create: (request: SaveLoanRequest) => apiClient.post<Loan>('/api/loans', request),
  update: (id: string, request: SaveLoanRequest) => apiClient.put<Loan>(`/api/loans/${id}`, request),
  remove: (id: string) => apiClient.delete<void>(`/api/loans/${id}`),
}
