import { api } from './client'
import type {
  AppConfig, CreateUserInput, DashboardData, HistoryRow, MasterDataInput, MasterDataRow, MasterKind, Me, Option,
  PagedResult, UpdateUserInput, UserRow,
} from './types'

// One small function per API call. Components never build URLs themselves.
export const authApi = {
  me: () => api.get<Me>('/api/auth/me', { allow401: true }),
  login: (userName: string, password: string) =>
    api.post<Me>('/api/auth/login', { userName, password }, { allow401: true }),
  logout: () => api.post<void>('/api/auth/logout'),
  /** Asks the server whether the cookie is still valid. A 401 triggers the "session ended" handler. */
  checkSession: () => api.get<Me>('/api/auth/me'),
  changePassword: (body: { currentPassword: string; newPassword: string; confirmPassword: string }) =>
    api.post<{ message: string }>('/api/auth/change-password', body),
  register: (body: { userName: string; password: string; firstName: string; lastName: string; employeeNo: string }) =>
    api.post<{ message: string }>('/api/auth/register', body),
}

export const configApi = { get: () => api.get<AppConfig>('/api/config') }
export const dashboardApi = { get: () => api.get<DashboardData>('/api/dashboard') }
export const historyApi = {
  list: (page: number, pageSize: number) =>
    api.get<PagedResult<HistoryRow>>(`/api/history?page=${page}&pageSize=${pageSize}`),
}

// Products, components and points share the same URL shape: /api/<kind>, /api/<kind>/{id}, ...
export const masterDataApi = {
  list: (kind: MasterKind, query: string) => api.get<PagedResult<MasterDataRow>>(`/api/${kind}?${query}`),
  options: (kind: MasterKind, type?: string) =>
    api.get<Option[]>(`/api/${kind}/options${type ? `?type=${type}` : ''}`),
  create: (kind: MasterKind, body: MasterDataInput) => api.post<MasterDataRow>(`/api/${kind}`, body),
  update: (kind: MasterKind, id: number, body: MasterDataInput) => api.put<MasterDataRow>(`/api/${kind}/${id}`, body),
  remove: (kind: MasterKind, id: number) => api.delete<{ archived: boolean }>(`/api/${kind}/${id}`),
  restore: (kind: MasterKind, id: number) => api.post<MasterDataRow>(`/api/${kind}/${id}/restore`),
}

export const usersApi = {
  list: (query: string) => api.get<PagedResult<UserRow>>(`/api/users?${query}`),
  create: (body: CreateUserInput) => api.post<UserRow>('/api/users', body),
  update: (id: number, body: UpdateUserInput) => api.put<UserRow>(`/api/users/${id}`, body),
  toggleActive: (id: number) => api.post<UserRow>(`/api/users/${id}/toggle-active`),
  resetPassword: (id: number, newPassword: string) => api.post<void>(`/api/users/${id}/reset-password`, { newPassword }),
}
