import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, Navigate, useNavigate } from 'react-router'
import { useAuth } from '../auth/auth-context'
import { ErrorAlert } from '../components/Alerts'
import { homeFor } from '../lib/capabilities'

// zod describes the rules once; react-hook-form runs them in the browser for fast feedback.
// (The server validates again: it is the authority.)
const schema = z.object({
  userName: z.string().min(1, 'User name is required'),
  password: z.string().min(1, 'Password is required'),
})
type FormValues = z.infer<typeof schema>

// Shown ONLY in development builds (import.meta.env.DEV is false in `npm run build`). Demo passwords only.
const DEMO_ACCOUNTS = [
  { userName: 'admin', role: 'ADMIN', password: 'Admin@12345' },
  { userName: 'tracker', role: 'COMPONENT_TRACKER', password: 'Demo@12345' },
  { userName: 'receiver', role: 'RECEIVE_USER', password: 'Demo@12345' },
  { userName: 'deliverer', role: 'DELIVERY_USER', password: 'Demo@12345' },
  { userName: 'viewer', role: 'REPORT_VIEWER', password: 'Demo@12345' },
]

export function LoginPage() {
  const { user, login, sessionEnded } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<unknown>(null)
  const { register, handleSubmit, setValue, formState: { errors, isSubmitting } } =
    useForm<FormValues>({ resolver: zodResolver(schema) })

  if (user) return <Navigate to={homeFor(user)} replace />

  async function onSubmit(values: FormValues) {
    setError(null)
    try {
      const me = await login(values.userName, values.password)
      navigate(homeFor(me), { replace: true })
    } catch (e) {
      setError(e)
    }
  }

  return (
    <div className="container py-5" style={{ maxWidth: 480 }}>
      <h1 className="h3 mb-4">Inventory Management System</h1>
      {sessionEnded && <div className="alert alert-warning">Your session has ended. Please sign in again.</div>}
      <ErrorAlert error={error} />
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <div className="mb-3">
          <label htmlFor="userName" className="form-label">User name</label>
          <input id="userName" className={`form-control ${errors.userName ? 'is-invalid' : ''}`}
            autoComplete="username" {...register('userName')} />
          <div className="invalid-feedback">{errors.userName?.message}</div>
        </div>
        <div className="mb-3">
          <label htmlFor="password" className="form-label">Password</label>
          <input id="password" type="password" className={`form-control ${errors.password ? 'is-invalid' : ''}`}
            autoComplete="current-password" {...register('password')} />
          <div className="invalid-feedback">{errors.password?.message}</div>
        </div>
        <button className="btn btn-primary" type="submit" disabled={isSubmitting}>Sign in</button>
        <Link className="ms-3" to="/register">Create an account</Link>
      </form>

      {import.meta.env.DEV && (
        <div className="card mt-4">
          <div className="card-header">Demo accounts (development only)</div>
          <table className="table table-sm mb-0">
            <thead><tr><th>User</th><th>Role</th><th>Password</th><th /></tr></thead>
            <tbody>
              {DEMO_ACCOUNTS.map((a) => (
                <tr key={a.userName}>
                  <td>{a.userName}</td><td>{a.role}</td><td><code>{a.password}</code></td>
                  <td><button type="button" className="btn btn-sm btn-outline-secondary"
                    onClick={() => { setValue('userName', a.userName); setValue('password', a.password) }}>Fill</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
