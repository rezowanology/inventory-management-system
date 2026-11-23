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
  /** The REVERSED row that cancelled this row (null = not reversed). */
  reversedById: number | null
  reversedBy: string | null
  reversedAt: string | null
  reversed: boolean
  /** Only true for admins, and only when the reversal rules allow it right now. */
  canReverse: boolean
}

export interface StuckUnit { barcode: string; productName: string; pointName: string; daysInStock: number }

export interface DashboardData {
  totalUnits: number
  stockCount: number
  todayReceived: number
  todayDelivered: number
  todayReversed: number
  unitsRegisteredToday: number
  stockByPoint: PointCount[]
  stuckLongest: StuckUnit[]
  /** Empty for roles without history access. */
  recent: HistoryRow[]
}

// ---- Day 5: audit log ----

export interface AuditRow {
  id: number
  createdAt: string
  actorId: number | null
  actor: string
  action: string
  entityType: string
  entityId: string
  entityRepr: string
  ipAddress: string | null
  hasValues: boolean
}

export interface AuditDetail extends Omit<AuditRow, 'hasValues'> {
  oldValues: Record<string, unknown> | null
  newValues: Record<string, unknown> | null
}

export interface AuditFilterOptions { actors: Option[]; actions: string[]; entityTypes: string[] }

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

export type UnitStatusName = 'NotFound' | 'Registered' | 'InStock' | 'Delivered'

export interface UnitEvent {
  id: number
  eventType: string
  pointName: string
  componentName: string | null
  componentBarcode: string | null
  performedBy: string
  occurredAt: string
}

export interface AttachedComponent {
  componentName: string
  componentBarcode: string
  trackedAt: string
}

/** Answer of GET /api/units/lookup and of every ops call. */
export interface UnitLookup {
  barcode: string
  exists: boolean
  product: string | null
  status: UnitStatusName
  currentPoint: string | null
  since: string | null
  components: AttachedComponent[]
  recentEvents: UnitEvent[]
}

export interface TrackInput {
  barcode: string
  productId?: number
  trackPointId: number
  components: { componentId: number; componentBarcode: string }[]
}

export interface HistoryFilterOptions {
  products: Option[]
  points: Option[]
  users: Option[]
}

// ---- Day 4: reversals ----

export interface ReversePreview {
  row: HistoryRow
  allowed: boolean
  blockedReason: string | null
  effect: string
}

export interface ReverseResult {
  reversalId: number
  unitBarcode: string
  reversedEventType: string
  effect: string
}

// ---- Day 4: reports ----

export interface StockRow {
  barcode: string
  productName: string
  pointId: number
  pointName: string
  receivedAt: string
  receivedBy: string
  daysInStock: number
  componentCount: number
}

export interface PointCount { pointId: number; pointName: string; count: number }

export interface StockReport {
  items: StockRow[]
  page: number
  pageSize: number
  totalCount: number
  byPoint: PointCount[]
}

export interface SummaryDay { day: string; tracked: number; received: number; delivered: number; reversed: number }

export interface SummaryReport {
  from: string
  to: string
  totals: { tracked: number; received: number; delivered: number; reversed: number }
  chart: SummaryDay[]
  items: SummaryDay[]
  page: number
  pageSize: number
  totalCount: number
}

export interface UnitMatch { barcode: string; productName: string; status: UnitStatusName }

export interface BarcodeReport {
  query: string
  found: boolean
  unit: UnitLookup | null
  matches: UnitMatch[]
  timeline: HistoryRow[]
}
