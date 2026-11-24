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

  it('recovers from a stale antiforgery token: fetches a fresh one and retries the POST once', async () => {
    const calls: string[] = []
    vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
      calls.push((init?.method ?? 'GET') + ' ' + url)
      if (url === '/api/auth/csrf') return new Response(null, { status: 204 })
      const firstTry = calls.filter((c) => c.startsWith('POST')).length === 1
      return firstTry
        ? new Response(JSON.stringify({ title: 'Invalid antiforgery token', detail: 'Reload the page and try again.' }), { status: 400, headers: { 'Content-Type': 'application/json' } })
        : new Response(JSON.stringify({ ok: true }), { status: 200, headers: { 'Content-Type': 'application/json' } })
    }))

    await expect(request('/api/auth/login', { method: 'POST', body: { userName: 'a', password: 'b' }, allow401: true })).resolves.toEqual({ ok: true })
    expect(calls.filter((c) => c.startsWith('POST'))).toHaveLength(2) // the original call and exactly one retry
    expect(calls.at(-2)).toBe('GET /api/auth/csrf') // a fresh token was fetched right before the retry
  })

  it('does not retry forever: a second antiforgery error is thrown', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => url === '/api/auth/csrf'
      ? new Response(null, { status: 204 })
      : new Response(JSON.stringify({ title: 'Invalid antiforgery token' }), { status: 400, headers: { 'Content-Type': 'application/json' } })))

    await expect(request('/api/ops/receive', { method: 'POST', body: {} })).rejects.toMatchObject({ status: 400, title: 'Invalid antiforgery token' })
  })
})
