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
