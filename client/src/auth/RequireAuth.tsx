import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { useAuth } from './auth-context'

/** Route guard: signed-out visitors are sent to /login (and brought back afterwards). */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth()
  const location = useLocation()
  if (loading) return <div className="p-5 text-center text-secondary">Loading...</div>
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return children
}
