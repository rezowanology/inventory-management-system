// Shapes of the JSON the API sends (camelCase). Mirrors src/Ims.Api/Contracts/Dtos.cs.
export interface Me {
  id: number
  userName: string
  displayName: string
  role: string
  employeeNo: string
  capabilities: string[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface HistoryRow {
  id: number
  unitBarcode: string
  productName: string
  eventType: string
  pointName: string
  componentName: string | null
  componentBarcode: string | null
  reversesId: number | null
  reason: string
  performedByEmployeeNo: string
  performedByName: string
  occurredAt: string
}

export interface DashboardData {
  totalUnits: number
  stockCount: number
}

export interface AppConfig {
  timeZone: string
}

export type Role = 'ADMIN' | 'COMPONENT_TRACKER' | 'RECEIVE_USER' | 'DELIVERY_USER' | 'REPORT_VIEWER'
export type PointType = 'TRACK' | 'RECEIVE' | 'DELIVERY'
export type MasterKind = 'products' | 'components' | 'points'

export interface MasterDataRow {
  id: number
  name: string
  isDeleted: boolean
  deletedAt: string | null
  createdAt: string
  updatedAt: string
  pointType: PointType | null
}

export interface MasterDataInput {
  name: string
  type?: PointType
}

export interface Option {
  id: number
  name: string
}

export interface UserRow {
  id: number
  userName: string
  firstName: string
  lastName: string
  employeeNo: string
  role: Role
  isActive: boolean
  lastLoginAt: string | null
  isPending: boolean
}

export interface CreateUserInput {
  userName: string
  firstName: string
  lastName: string
  employeeNo: string
  role: Role
  password: string
}

export interface UpdateUserInput {
  firstName: string
  lastName: string
  employeeNo: string
  role: Role
}
