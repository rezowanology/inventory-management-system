import { act, renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter, useLocation } from 'react-router'
import { describe, expect, it } from 'vitest'
import { parseListParams, toQueryString, useListParams } from './useListParams'

describe('parseListParams', () => {
  it('reads q, page and filters, using defaults for missing filters', () => {
    const p = parseListParams(new URLSearchParams('q=fridge&page=3&role=ADMIN'), ['role', 'status'], { status: 'active' })
    expect(p).toEqual({ q: 'fridge', page: 3, filters: { role: 'ADMIN', status: 'active' } })
  })

  it('repairs a bad page number', () => {
    expect(parseListParams(new URLSearchParams('page=abc')).page).toBe(1)
    expect(parseListParams(new URLSearchParams('page=-4')).page).toBe(1)
    expect(parseListParams(new URLSearchParams('')).page).toBe(1)
  })
})

describe('toQueryString', () => {
  it('leaves out empty values', () => {
    expect(toQueryString({ q: 'a b', page: 2, status: '', type: undefined })).toBe('q=a+b&page=2')
  })
})

describe('useListParams (state lives in the URL)', () => {
  // The hook is rendered inside a router; the Spy component exposes the current URL so the test can read it.
  let currentSearch = ''
  function Spy() { currentSearch = useLocation().search; return null }
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <MemoryRouter initialEntries={['/manage/products?q=old&page=4&status=all']}>
        <Spy />{children}
      </MemoryRouter>
    )
  }

  it('round-trips the URL params and resets the page when search or a filter changes', () => {
    const { result } = renderHook(() => useListParams(['status'], { status: 'active' }), { wrapper: Wrapper })
    expect(result.current.params).toEqual({ q: 'old', page: 4, filters: { status: 'all' } })

    act(() => result.current.setPage(5))
    expect(new URLSearchParams(currentSearch).get('page')).toBe('5')
    expect(result.current.params.page).toBe(5)

    act(() => result.current.setQ(' new '))
    expect(new URLSearchParams(currentSearch).get('q')).toBe('new')
    expect(new URLSearchParams(currentSearch).has('page')).toBe(false) // back to page 1

    act(() => result.current.setFilter('status', 'active')) // the default value is not written to the URL
    expect(new URLSearchParams(currentSearch).has('status')).toBe(false)
    expect(result.current.params.filters.status).toBe('active')
  })
})
