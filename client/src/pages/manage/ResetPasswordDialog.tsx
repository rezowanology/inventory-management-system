import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { UserRow } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { Modal } from '../../components/Modal'
import { applyServerErrors } from '../../lib/serverErrors'

const schema = z.object({ newPassword: z.string().min(8, 'At least 8 characters') })
type FormValues = z.infer<typeof schema>

/** Admin sets a new password for someone else. The old password is never shown. */
export function ResetPasswordDialog({ user, onSubmit, onClose }: {
  user: UserRow
  onSubmit: (newPassword: string) => Promise<unknown>
  onClose: () => void
}) {
  const [generalError, setGeneralError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } =
    useForm<FormValues>({ resolver: zodResolver(schema) })

  async function submit(v: FormValues) {
    setGeneralError(null)
    try { await onSubmit(v.newPassword) } catch (e) { setGeneralError(applyServerErrors(e, setError, ['newPassword'])) }
  }

  return (
    <Modal title={`Reset password for ${user.userName}`} onClose={onClose}>
      <form onSubmit={handleSubmit(submit)} noValidate>
        <div className="modal-body">
          {generalError && <ErrorAlert error={new Error(generalError)} />}
          <label htmlFor="rp-password" className="form-label">New password</label>
          <input id="rp-password" type="password" autoComplete="new-password" autoFocus
            className={`form-control ${errors.newPassword ? 'is-invalid' : ''}`} {...register('newPassword')} />
          <div className="invalid-feedback">{errors.newPassword?.message}</div>
          <div className="form-text">Any session this user has open is ended; they sign in with the new password.</div>
        </div>
        <div className="modal-footer">
          <button type="button" className="btn btn-outline-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>Set password</button>
        </div>
      </form>
    </Modal>
  )
}
