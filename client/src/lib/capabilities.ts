import type { Me } from '../api/types'

// Capability NAMES (not the matrix!). The server decides who has which capability and sends the list in
// GET /api/auth/me. The client only asks "does the list contain X?".
export const CAP = {
  dashboard: 'dashboard',
  changePassword: 'account.change-password',
  opsTrack: 'ops.track',
  opsReceive: 'ops.receive',
  opsDeliver: 'ops.deliver',
  historyView: 'history.view',
  historyReverse: 'history.reverse',
  reportsView: 'reports.view',
  masterData: 'masterdata.manage',
  users: 'users.manage',
  audit: 'audit.view',
} as const

export function can(me: Me | null | undefined, capability: string): boolean {
  return !!me && me.capabilities.includes(capability)
}

/** Where to go right after login: operational roles land on their own scan page (A7). */
export function homeFor(me: Me): string {
  switch (me.role) {
    case 'COMPONENT_TRACKER': return '/ops/track'
    case 'RECEIVE_USER': return '/ops/receive'
    case 'DELIVERY_USER': return '/ops/deliver'
    default: return '/'
  }
}
