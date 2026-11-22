import { api } from './client'
import type { AppConfig, DashboardData, HistoryRow, Me, PagedResult } from './types'

// One small function per API call. Components never build URLs themselves.
export const authApi = {
  me: () => api.get<Me>('/api/auth/me', { allow401: true }),
  login: (userName: string, password: string) =>
    api.post<Me>('/api/auth/login', { userName, password }, { allow401: true }),
  logout: () => api.post<void>('/api/auth/logout'),
  register: (body: { userName: string; password: string; firstName: string; lastName: string; employeeNo: string }) =>
    api.post<{ message: string }>('/api/auth/register', body),
}

export const configApi = { get: () => api.get<AppConfig>('/api/config') }
export const dashboardApi = { get: () => api.get<DashboardData>('/api/dashboard') }
export const historyApi = {
  list: (page: number, pageSize: number) =>
    api.get<PagedResult<HistoryRow>>(`/api/history?page=${page}&pageSize=${pageSize}`),
}
