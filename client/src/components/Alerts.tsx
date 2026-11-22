import { ApiError } from '../api/client'

/** Shows an API error (or any Error) in a Bootstrap alert. */
export function ErrorAlert({ error }: { error: unknown }) {
  if (!error) return null
  const message = error instanceof ApiError ? error.detail ?? error.title : error instanceof Error ? error.message : String(error)
  return <div className="alert alert-danger" role="alert">{message}</div>
}
