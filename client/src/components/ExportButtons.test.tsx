import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { toQueryString } from '../hooks/useListParams'
import { ExportButtons, exportUrl } from './ExportButtons'

describe('ExportButtons', () => {
  it('builds the download URL from the same list parameters as the screen', () => {
    const query = toQueryString({ q: 'fridge', page: 3, pageSize: 20, eventType: 'RECEIVED', from: '' })
    expect(exportUrl('/api/history', query, 'csv')).toBe('/api/history?q=fridge&page=3&pageSize=20&eventType=RECEIVED&format=csv')
    expect(exportUrl('/api/history', query, 'xlsx')).toMatch(/&format=xlsx$/)
  })

  it('works without any filters', () => {
    expect(exportUrl('/api/users', '', 'xlsx')).toBe('/api/users?format=xlsx')
  })

  it('renders two real links the browser can download', () => {
    render(<ExportButtons path="/api/reports/stock" query="minDays=3" />)
    expect(screen.getByRole('link', { name: 'Export CSV' })).toHaveAttribute('href', '/api/reports/stock?minDays=3&format=csv')
    expect(screen.getByRole('link', { name: 'Export Excel' })).toHaveAttribute('href', '/api/reports/stock?minDays=3&format=xlsx')
  })

  it('can be switched off', () => {
    render(<ExportButtons path="/api/reports/summary" query="" disabled />)
    expect(screen.getByRole('link', { name: 'Export CSV' })).toHaveClass('disabled')
  })
})
