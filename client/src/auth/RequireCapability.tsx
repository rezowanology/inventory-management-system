import type { ReactNode } from 'react'
import { Navigate } from 'react-router'
import { can } from '../lib/capabilities'
import { useAuth } from './auth-context'

/**
 * Route guard: shows the page only if the server-provided capability list contains `capability`.
 * (The server enforces the same rule on the API; this just gives a friendly Forbidden page.)
 */
export function RequireCapability({ capability, children }: { capability: string; children: ReactNode }) {
  const { user } = useAuth()
  if (!can(user, capability)) return <Navigate to="/forbidden" replace />
  return children
}
