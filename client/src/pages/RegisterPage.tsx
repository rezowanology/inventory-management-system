import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link } from 'react-router'
import { authApi } from '../api/endpoints'
import { ApiError } from '../api/client'
import { ErrorAlert } from '../components/Alerts'

const schema = z.object({
  userName: z.string().min(3, 'At least 3 characters'),
  firstName: z.string().min(1, 'Required'),
  lastName: z.string().min(1, 'Required'),
  employeeNo: z.string().min(2, 'Required'),
  password: z.string().min(8, 'At least 8 characters'),
})
type FormValues = z.infer<typeof schema>

const FIELDS: { name: keyof FormValues; label: string; type?: string }[] = [
  { name: 'userName', label: 'User name' },
  { name: 'firstName', label: 'First name' },
  { name: 'lastName', label: 'Last name' },
  { name: 'employeeNo', label: 'Employee number' },
  { name: 'password', label: 'Password', type: 'password' },
]

export function RegisterPage() {
  const [done, setDone] = useState<string | null>(null)
  const [error, setError] = useState<unknown>(null)
  const { register, handleSubmit, setError: setFieldError, formState: { errors, isSubmitting } } =
    useForm<FormValues>({ resolver: zodResolver(schema) })

  async function onSubmit(values: FormValues) {
    setError(null)
    try {
      const result = await authApi.register(values)
      setDone(result.message)
    } catch (e) {
      // Copy server field errors (keys like "employeeNo") into the matching form fields.
      if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0) {
        for (const [field, messages] of Object.entries(e.fieldErrors)) {
          const name = field.charAt(0).toLowerCase() + field.slice(1)
          if (name in schema.shape) setFieldError(name as keyof FormValues, { message: messages.join(' ') })
        }
      } else setError(e)
    }
  }

  if (done) {
    return (
      <div className="container py-5" style={{ maxWidth: 480 }}>
        <div className="alert alert-success">{done}</div>
        <Link to="/login">Back to sign in</Link>
      </div>
    )
  }

  return (
    <div className="container py-5" style={{ maxWidth: 480 }}>
      <h1 className="h3 mb-4">Create an account</h1>
      <p className="text-secondary">New accounts are Report Viewers and stay inactive until an Admin activates them.</p>
      <ErrorAlert error={error} />
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        {FIELDS.map((f) => (
          <div className="mb-3" key={f.name}>
            <label htmlFor={f.name} className="form-label">{f.label}</label>
            <input id={f.name} type={f.type ?? 'text'} className={`form-control ${errors[f.name] ? 'is-invalid' : ''}`}
              {...register(f.name)} />
            <div className="invalid-feedback">{errors[f.name]?.message}</div>
          </div>
        ))}
        <button className="btn btn-primary" type="submit" disabled={isSubmitting}>Register</button>
        <Link className="ms-3" to="/login">Back to sign in</Link>
      </form>
    </div>
  )
}
