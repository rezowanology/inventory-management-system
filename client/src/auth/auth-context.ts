import { createContext, useContext } from 'react'
import type { Me } from '../api/types'

export interface AuthState {
  /** The signed-in user, or null when signed out. */
  user: Me | null
  loading: boolean
  /** True when the server ended our session (e.g. the account was deactivated). */
  sessionEnded: boolean
  login: (userName: string, password: string) => Promise<Me>
  logout: () => Promise<void>
}

// A React Context lets any component read the current user without passing props down every level.
export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}
