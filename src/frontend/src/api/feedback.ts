import { apiClient } from './client'

export type FeedbackStatus = 'Open' | 'InProgress' | 'Resolved'

export interface FeedbackMessage {
  id: string
  authorName: string
  isFromAdmin: boolean
  body: string
  createdAtUtc: string
}

export interface Feedback {
  id: string
  subject: string
  status: FeedbackStatus
  /** Only populated on the admin views. */
  submittedByName: string
  submittedByEmail: string
  createdAtUtc: string
  updatedAtUtc: string
  resolvedAtUtc: string | null
  messageCount: number
  /** False once resolved — the reply box hides rather than inviting a rejection. */
  canReply: boolean
  messages: FeedbackMessage[]
}

export interface FeedbackList {
  items: Feedback[]
  totalCount: number
  openCount: number
  inProgressCount: number
}

export const feedbackApi = {
  listMine: () => apiClient.get<Feedback[]>('/api/feedback'),
  getMine: (id: string) => apiClient.get<Feedback>(`/api/feedback/${id}`),
  submit: (subject: string, message: string) =>
    apiClient.post<Feedback>('/api/feedback', { subject, message }),
  reply: (id: string, body: string) =>
    apiClient.post<Feedback>(`/api/feedback/${id}/replies`, { body }),
}

export const adminFeedbackApi = {
  list: (status?: FeedbackStatus) =>
    apiClient.get<FeedbackList>(`/api/admin/feedback${status ? `?status=${status}` : ''}`),
  get: (id: string) => apiClient.get<Feedback>(`/api/admin/feedback/${id}`),
  reply: (id: string, body: string) =>
    apiClient.post<Feedback>(`/api/admin/feedback/${id}/replies`, { body }),
  setStatus: (id: string, status: FeedbackStatus) =>
    apiClient.put<Feedback>(`/api/admin/feedback/${id}/status`, { status }),
}

export const statusLabel: Record<FeedbackStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  Resolved: 'Resolved',
}

/** Blue = being handled, green = done, muted = waiting. Same three colours as everywhere else. */
export const statusClass: Record<FeedbackStatus, string> = {
  Open: 'bg-raised text-ink-soft',
  InProgress: 'bg-brand-100 text-brand-700 dark:bg-brand-950 dark:text-brand-300',
  Resolved: 'bg-positive-100 text-positive-700 dark:bg-positive-950 dark:text-positive-400',
}
