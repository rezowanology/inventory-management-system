import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { describe, expect, it } from 'vitest'
import type { Me } from '../api/types'
import { AuthContext } from './auth-context'
import { RequireCapability } from './RequireCapability'

function renderGuard(capabilities: string[]) {
  const user: Me = { id: 1, userName: 'viewer', displayName: 'Vera Viewer', role: 'REPORT_VIEWER', employeeNo: '20001', capabilities }
  render(
    <AuthContext.Provider value={{ user, loading: false, sessionEnded: false, login: async () => user, logout: async () => {} }}>
      <MemoryRouter initialEntries={['/ops/receive']}>
        <Routes>
          <Route path="/ops/receive" element={
            <RequireCapability capability="ops.receive"><div>Receive screen</div></RequireCapability>} />
          <Route path="/forbidden" element={<div>Forbidden page</div>} />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
}

describe('RequireCapability', () => {
  it('redirects to /forbidden when the capability is missing', () => {
    renderGuard(['dashboard', 'history.view'])
    expect(screen.getByText('Forbidden page')).toBeInTheDocument()
    expect(screen.queryByText('Receive screen')).not.toBeInTheDocument()
  })

  it('shows the page when the capability is present', () => {
    renderGuard(['dashboard', 'ops.receive'])
    expect(screen.getByText('Receive screen')).toBeInTheDocument()
  })
})
