import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ErrorAlert } from '../../components/Alerts'
import { applyServerErrors } from '../../lib/serverErrors'

// Same rule as the server (InventoryService.ReverseAsync): at least 5 characters after trimming.
// zodResolver runs this schema before submit, so a short reason never reaches the API.
export const reverseSchema = z.object({
  reason: z.string().trim().min(5, 'Give a reason of at least 5 characters').max(500, 'The reason can have at most 500 characters'),
})
type FormValues = z.infer<typeof reverseSchema>

/** The reason box + confirm button of the reversal screen. */
export function ReverseForm({ onSubmit }: { onSubmit: (reason: string) => Promise<unknown> }) {
  const [generalError, setGeneralError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(reverseSchema),
    defaultValues: { reason: '' },
  })

  async function submit(values: FormValues) {
    setGeneralError(null)
    try {
      await onSubmit(values.reason)
    } catch (e) {
      // A field error from the server goes under the box; anything else (e.g. "blocked") into the red alert.
      setGeneralError(applyServerErrors(e, setError, ['reason']))
    }
  }

  return (
    <form onSubmit={handleSubmit(submit)} noValidate>
      {generalError && <ErrorAlert error={new Error(generalError)} />}
      <div className="mb-3">
        <label htmlFor="reverse-reason" className="form-label fw-semibold">Reason (required)</label>
        <textarea id="reverse-reason" rows={3} className={`form-control ${errors.reason ? 'is-invalid' : ''}`}
          placeholder="Why is this row being reversed?" {...register('reason')} />
        <div className="invalid-feedback">{errors.reason?.message}</div>
      </div>
      <button type="submit" className="btn btn-danger" disabled={isSubmitting}>Reverse this row</button>
    </form>
  )
}
