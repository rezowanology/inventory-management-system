import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import { ScanErrorAlert } from './Alerts'
import { ScanPanel } from './ScanPanel'

afterEach(() => vi.unstubAllGlobals())

function Harness({ onCommit }: { onCommit: (b: string) => void }) {
  const [committed, setCommitted] = useState('')
  return <ScanPanel committed={committed} onCommit={(b) => { setCommitted(b); onCommit(b) }} />
}

function renderPanel(onCommit: (b: string) => void) {
  // The fake fetch answers both /api/config and /api/units/lookup.
  vi.stubGlobal('fetch', vi.fn(async (url: string) => new Response(JSON.stringify(
    url.includes('/api/config') ? { timeZone: 'UTC' } : {
      barcode: 'TESTUNIT0001', exists: false, product: null, status: 'NotFound', currentPoint: null, since: null, components: [], recentEvents: [],
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={client}><Harness onCommit={onCommit} /></QueryClientProvider>)
}

describe('ScanPanel', () => {
  it('upper-cases what is typed and submits the barcode on Enter', async () => {
    const onCommit = vi.fn()
    renderPanel(onCommit)
    const input = screen.getByLabelText('Unit barcode')
    expect(input).toHaveFocus() // ready for the scanner

    await userEvent.type(input, 'testunit0001{Enter}')

    expect(input).toHaveValue('TESTUNIT0001')
    expect(onCommit).toHaveBeenCalledWith('TESTUNIT0001')
    expect(await screen.findByTestId('unit-card')).toHaveTextContent('New barcode') // NotFound badge
  })

  it('does not look up a barcode with a bad shape', async () => {
    const onCommit = vi.fn()
    renderPanel(onCommit)
    await userEvent.type(screen.getByLabelText('Unit barcode'), 'ab{Enter}')
    expect(onCommit).not.toHaveBeenCalled()
    expect(await screen.findByText(/must be 6-32 characters/)).toBeInTheDocument()
  })
})

describe('ScanErrorAlert', () => {
  it('shows a 409 duplicate scan as a yellow warning with where, when and by', () => {
    const error = new ApiError({
      status: 409, title: 'Duplicate scan', type: 'duplicate-scan', detail: 'Unit TESTUNIT0001 is already in stock.',
      extras: { when: '2026-03-28T04:15:00Z', where: 'Refrigerator Receive Point 1', by: '58877 Rahim Uddin' },
    })
    render(<ScanErrorAlert error={error} timeZone="Asia/Dhaka" />)
    const alert = screen.getByRole('alert')
    expect(alert).toHaveClass('alert-warning')
    expect(alert).toHaveTextContent('already in stock')
    expect(alert).toHaveTextContent('Refrigerator Receive Point 1')
    expect(alert).toHaveTextContent('2026-03-28 10:15:00') // shown in the configured zone
    expect(alert).toHaveTextContent('58877 Rahim Uddin')
    expect(alert).toHaveTextContent('Nothing was saved')
  })

  it('shows a 422 rule violation as a red error', () => {
    render(<ScanErrorAlert error={new ApiError({ status: 422, title: 'Business rule violated', detail: 'Unit X is not in stock.' })} />)
    expect(screen.getByRole('alert')).toHaveClass('alert-danger')
  })
})
