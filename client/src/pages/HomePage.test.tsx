import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Me } from '../api/types'
import { AuthContext } from '../auth/auth-context'
import { HomePage } from './HomePage'

afterEach(() => vi.unstubAllGlobals())

const DASHBOARD = {
  totalUnits: 30, stockCount: 12, todayReceived: 3, todayDelivered: 2, todayReversed: 1, unitsRegisteredToday: 4,
  stockByPoint: [{ pointId: 1, pointName: 'Refrigerator Receive Point 1', count: 12 }],
  stuckLongest: [{ barcode: 'DEMO000001', productName: 'Freezer', pointName: 'Refrigerator Receive Point 1', daysInStock: 27 }],
  recent: [],
}

function renderAs(role: string, capabilities: string[]) {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => new Response(JSON.stringify(
    url.includes('/api/config') ? { timeZone: 'UTC' } : DASHBOARD), { status: 200, headers: { 'Content-Type': 'application/json' } })))
  const me: Me = { id: 1, userName: role, displayName: role, role, employeeNo: '1', capabilities }
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <AuthContext.Provider value={{ user: me, loading: false, sessionEnded: false, login: vi.fn(), logout: vi.fn() }}>
      <QueryClientProvider client={client}><MemoryRouter><HomePage /></MemoryRouter></QueryClientProvider>
    </AuthContext.Provider>)
}

describe('Dashboard', () => {
  it('shows the tiles, the stock-by-point table and the stuck list', async () => {
    renderAs('ADMIN', ['dashboard', 'ops.track', 'ops.receive', 'ops.deliver', 'history.view', 'reports.view', 'users.manage', 'audit.view'])
    await waitFor(() => expect(screen.getByTestId('today-received')).toHaveTextContent('3'))
    expect(screen.getByTestId('stock-count')).toHaveTextContent('12')
    expect(screen.getByText('Stock by receive point')).toBeInTheDocument()
    expect(screen.getByText('27')).toBeInTheDocument()
  })

  it('offers only the quick actions of the signed-in capabilities', async () => {
    renderAs('RECEIVE_USER', ['dashboard', 'ops.receive', 'ops.lookup'])
    await screen.findByTestId('stock-count')
    expect(screen.getByRole('link', { name: 'Receive units' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Track components' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Audit log' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Stock report' })).not.toBeInTheDocument()
  })

  it('links a stuck barcode to its history only for roles that may open reports', async () => {
    const viewer = renderAs('REPORT_VIEWER', ['dashboard', 'history.view', 'reports.view'])
    expect(await screen.findByRole('link', { name: 'DEMO000001' })).toHaveAttribute('href', '/reports/barcode?barcode=DEMO000001')
    viewer.unmount()

    renderAs('DELIVERY_USER', ['dashboard', 'ops.deliver'])
    await screen.findByText('DEMO000001')
    expect(screen.queryByRole('link', { name: 'DEMO000001' })).not.toBeInTheDocument()
  })
})
