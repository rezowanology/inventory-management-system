// The ONLY place that calls fetch(). Every request goes through here so that cookies, the antiforgery
// header, JSON handling and error mapping are done once.

/** An error answered by the API (RFC 7807 ProblemDetails) turned into a JavaScript Error. */
export class ApiError extends Error {
  status: number
  title: string
  detail?: string
  code?: string
  /** ProblemDetails "type": a duplicate-scan warning has type "duplicate-scan" (HTTP 409). */
  type?: string
  /** Extra ProblemDetails members, e.g. { when, where, by } of a duplicate-scan warning. */
  extras: Record<string, unknown>
  fieldErrors: Record<string, string[]>

  constructor(init: {
    status: number; title: string; detail?: string; code?: string; type?: string
    extras?: Record<string, unknown>; fieldErrors?: Record<string, string[]>
  }) {
    super(init.detail ?? init.title)
    this.name = 'ApiError'
    this.status = init.status
    this.title = init.title
    this.detail = init.detail
    this.code = init.code
    this.type = init.type
    this.extras = init.extras ?? {}
    this.fieldErrors = init.fieldErrors ?? {}
  }
}

/** Turns a ProblemDetails (or any failed response) body into an ApiError. Exported for tests. */
export function toApiError(status: number, body: unknown): ApiError {
  const b = (body && typeof body === 'object' ? body : {}) as Record<string, unknown>
  return new ApiError({
    status,
    title: typeof b.title === 'string' ? b.title : `Request failed (${status})`,
    detail: typeof b.detail === 'string' ? b.detail : undefined,
    code: typeof b.code === 'string' ? b.code : undefined,
    type: typeof b.type === 'string' ? b.type : undefined,
    extras: { when: b.when, where: b.where, by: b.by },
    fieldErrors: (b.errors && typeof b.errors === 'object' ? b.errors : {}) as Record<string, string[]>,
  })
}

let onUnauthorized: (() => void) | null = null
/** AuthProvider registers this so a 401 anywhere clears the cached user and sends us to /login. */
export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

function readXsrfCookie(): string | undefined {
  const match = document.cookie.split('; ').find((c) => c.startsWith('XSRF-TOKEN='))
  return match ? decodeURIComponent(match.slice('XSRF-TOKEN='.length)) : undefined
}

/** Asks the server for a fresh antiforgery token (it arrives as the XSRF-TOKEN cookie). */
export async function fetchCsrf(): Promise<void> {
  await fetch('/api/auth/csrf', { credentials: 'same-origin' })
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** Do not treat a 401 as "session ended" (used by login and the first /me check). */
  allow401?: boolean
}

export async function request<T>(path: string, options: RequestOptions = {}, isRetry = false): Promise<T> {
  const { method = 'GET', body, allow401 = false } = options
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  if (method !== 'GET') {
    // Unsafe methods must carry the antiforgery token in a header (never in localStorage).
    if (!readXsrfCookie()) await fetchCsrf()
    const token = readXsrfCookie()
    if (token) headers['X-XSRF-TOKEN'] = token
  }

  const response = await fetch(path, {
    method,
    headers,
    credentials: 'same-origin',
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (response.ok) {
    if (response.status === 204) return undefined as T
    const text = await response.text()
    return (text ? JSON.parse(text) : undefined) as T
  }

  let errorBody: unknown
  try { errorBody = await response.json() } catch { errorBody = undefined }
  // A stale XSRF-TOKEN cookie (for example left over from before the database was reset or the keys changed) makes the server
  // answer 400 "Invalid antiforgery token". Fetch a fresh token and try ONCE more, so the user never has to clear cookies by hand.
  if (!isRetry && method !== 'GET' && response.status === 400 && (errorBody as { title?: string } | undefined)?.title === 'Invalid antiforgery token') {
    await fetchCsrf()
    return request<T>(path, options, true)
  }
  if (response.status === 401 && !allow401) onUnauthorized?.()
  throw toApiError(response.status, errorBody)
}

export const api = {
  get: <T>(path: string, options?: Omit<RequestOptions, 'method' | 'body'>) => request<T>(path, options),
  post: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'POST', body }),
  put: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
