import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, request, setUnauthorizedHandler, toApiError } from './client'

afterEach(() => {
  vi.unstubAllGlobals()
  setUnauthorizedHandler(null)
})

describe('toApiError', () => {
  it('maps a ProblemDetails body', () => {
    const err = toApiError(422, { title: 'Business rule violated', detail: 'Unit is not in stock', code: 'NOT_IN_STOCK' })
    expect(err).toBeInstanceOf(ApiError)
    expect(err.status).toBe(422)
    expect(err.title).toBe('Business rule violated')
    expect(err.detail).toBe('Unit is not in stock')
    expect(err.code).toBe('NOT_IN_STOCK')
    expect(err.message).toBe('Unit is not in stock')
  })

  it('maps validation field errors', () => {
    const err = toApiError(400, { title: 'One or more validation errors occurred.', errors: { employeeNo: ['Already used'] } })
    expect(err.fieldErrors).toEqual({ employeeNo: ['Already used'] })
  })

  it('copes with an empty body', () => {
    expect(toApiError(500, undefined).title).toBe('Request failed (500)')
  })
})

describe('request', () => {
  it('throws an ApiError built from the response and calls the 401 handler', async () => {
    vi.stubGlobal('fetch', vi.fn(async () =>
      new Response(JSON.stringify({ title: 'Unauthorized' }), { status: 401, headers: { 'Content-Type': 'application/json' } })))
    const handler = vi.fn()
    setUnauthorizedHandler(handler)

    await expect(request('/api/history')).rejects.toMatchObject({ status: 401, title: 'Unauthorized' })
    expect(handler).toHaveBeenCalledOnce()
  })

  it('does not call the 401 handler when allow401 is set (login)', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 401 })))
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    await expect(request('/api/auth/me', { allow401: true })).rejects.toBeInstanceOf(ApiError)
    expect(handler).not.toHaveBeenCalled()
  })
})
