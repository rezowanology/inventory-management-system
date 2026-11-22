import { ApiError } from '../api/client'
import { formatDateTime } from '../lib/dates'

/** Shows an API error (or any Error) in a Bootstrap alert. */
export function ErrorAlert({ error }: { error: unknown }) {
  if (!error) return null
  const message = error instanceof ApiError ? error.detail ?? error.title : error instanceof Error ? error.message : String(error)
  return <div className="alert alert-danger" role="alert">{message}</div>
}

/**
 * The alert for a failed scan operation. A 409 "duplicate-scan" is a WARNING (yellow): the unit is already in that
 * state, nothing was saved, and the server says where, when and by whom. Every other error is red.
 */
export function ScanErrorAlert({ error, timeZone = 'UTC' }: { error: unknown; timeZone?: string }) {
  if (!error) return null
  if (error instanceof ApiError && error.status === 409 && error.type === 'duplicate-scan') {
    const { when, where, by } = error.extras
    return (
      <div className="alert alert-warning" role="alert">
        <strong>Duplicate scan.</strong> {error.detail} Nothing was saved.
        {typeof where === 'string' && (
          <div className="small mt-1">
            At <strong>{where}</strong>{typeof when === 'string' && <> on {formatDateTime(when, timeZone)}</>}
            {typeof by === 'string' && <> by {by}</>}.
          </div>
        )}
      </div>
    )
  }
  return <ErrorAlert error={error} />
}
