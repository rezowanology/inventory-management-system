import type { MasterKind, PointType, Role } from '../../api/types'

export const POINT_TYPES: PointType[] = ['TRACK', 'RECEIVE', 'DELIVERY']
export const ROLES: Role[] = ['ADMIN', 'COMPONENT_TRACKER', 'RECEIVE_USER', 'DELIVERY_USER', 'REPORT_VIEWER']

export interface MasterKindConfig {
  kind: MasterKind
  title: string
  singular: string
  hasType: boolean
}

export const MASTER_KINDS: Record<MasterKind, MasterKindConfig> = {
  products: { kind: 'products', title: 'Products', singular: 'Product', hasType: false },
  components: { kind: 'components', title: 'Components', singular: 'Component', hasType: false },
  points: { kind: 'points', title: 'Points', singular: 'Point', hasType: true },
}
