import type { FieldPath, FieldValues, UseFormSetError } from 'react-hook-form'
import { ApiError } from '../api/client'

/**
 * Copies the field errors of an API answer (ProblemDetails "errors": { name: ["..."] }) into a react-hook-form
 * form with setError, so the message appears under the right input.
 * The server may send "Name" or "name"; we lower-case the first letter before matching the form field names.
 * Returns the general message when nothing could be placed on a field (show it in an alert), otherwise null.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly FieldPath<T>[],
): string | null {
  if (!(error instanceof ApiError)) return error instanceof Error ? error.message : String(error)

  let placed = 0
  for (const [key, messages] of Object.entries(error.fieldErrors)) {
    const field = fields.find((f) => f === key.charAt(0).toLowerCase() + key.slice(1))
    if (field && messages.length > 0) {
      setError(field, { type: 'server', message: messages[0] })
      placed++
    }
  }
  return placed > 0 ? null : (error.detail ?? error.title)
}
