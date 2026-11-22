import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { authApi } from '../api/endpoints'
import { ErrorAlert } from '../components/Alerts'
import { applyServerErrors } from '../lib/serverErrors'

const schema = z.object({
  currentPassword: z.string().min(1, 'Enter your current password'),
  newPassword: z.string().min(8, 'At least 8 characters'),
  confirmPassword: z.string().min(1, 'Repeat the new password'),
}).refine((v) => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: 'The two new passwords do not match' })
type FormValues = z.infer<typeof schema>

/** Every signed-in role may change their own password. */
export function ChangePasswordPage() {
  const [success, setSuccess] = useState(false)
  const [generalError, setGeneralError] = useState<string | null>(null)
  const { register, handleSubmit, setError, reset, formState: { errors, isSubmitting } } =
    useForm<FormValues>({ resolver: zodResolver(schema) })

  async function submit(values: FormValues) {
    setSuccess(false)
    setGeneralError(null)
    try {
      await authApi.changePassword(values)
      reset() // empty the three boxes
      setSuccess(true)
    } catch (e) {
      setGeneralError(applyServerErrors(e, setError, ['currentPassword', 'newPassword', 'confirmPassword']))
    }
  }

  const field = (name: keyof FormValues, label: string, autoComplete: string) => (
    <div className="mb-3">
      <label htmlFor={name} className="form-label">{label}</label>
      <input id={name} type="password" autoComplete={autoComplete}
        className={`form-control ${errors[name] ? 'is-invalid' : ''}`} {...register(name)} />
      <div className="invalid-feedback">{errors[name]?.message}</div>
    </div>
  )

  return (
    <div style={{ maxWidth: 480 }}>
      <h1 className="h4 mb-3">Change password</h1>
      {success && <div className="alert alert-success" role="status">Your password was changed.</div>}
      {generalError && <ErrorAlert error={new Error(generalError)} />}
      <form onSubmit={handleSubmit(submit)} noValidate>
        {field('currentPassword', 'Current password', 'current-password')}
        {field('newPassword', 'New password', 'new-password')}
        {field('confirmPassword', 'Repeat new password', 'new-password')}
        <button className="btn btn-primary" type="submit" disabled={isSubmitting}>Change password</button>
      </form>
    </div>
  )
}
