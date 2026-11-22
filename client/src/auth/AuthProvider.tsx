import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { authApi } from '../api/endpoints'
import { fetchCsrf, setUnauthorizedHandler } from '../api/client'
import type { Me } from '../api/types'
import { AuthContext, type AuthState } from './auth-context'

const ME_KEY = ['auth', 'me'] as const // React Query "key": the cache label for the current user

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [sessionEnded, setSessionEnded] = useState(false)

  // Load who we are once on start-up. The cookie (not JavaScript) keeps the session, so a page refresh
  // still finds the user signed in.
  const meQuery = useQuery({
    queryKey: ME_KEY,
    queryFn: async () => {
      try { return await authApi.me() } catch { return null } // 401 or network problem -> signed out
    },
    staleTime: Infinity,
    retry: false,
  })

  // Any later 401 means the server ended the session: forget the user; RequireAuth sends us to /login.
  useEffect(() => {
    setUnauthorizedHandler(() => {
      queryClient.setQueryData(ME_KEY, null)
      setSessionEnded(true)
    })
    return () => setUnauthorizedHandler(null)
  }, [queryClient])

  // Forget cached server data of the previous user, but keep the "me" query that components are watching
  // (queryClient.clear() would detach them and the screen would not update).
  const clearOtherData = useCallback(
    () => queryClient.removeQueries({ predicate: (q) => q.queryKey[0] !== ME_KEY[0] }),
    [queryClient],
  )

  const login = useCallback(async (userName: string, password: string) => {
    const me = await authApi.login(userName, password)
    await fetchCsrf() // the old antiforgery token belonged to the anonymous user; get a new one
    clearOtherData()
    queryClient.setQueryData<Me | null>(ME_KEY, me)
    setSessionEnded(false)
    return me
  }, [queryClient, clearOtherData])

  const logout = useCallback(async () => {
    try { await authApi.logout() } finally {
      await fetchCsrf()
      clearOtherData()
      queryClient.setQueryData<Me | null>(ME_KEY, null)
      setSessionEnded(false)
    }
  }, [queryClient, clearOtherData])

  const value = useMemo<AuthState>(
    () => ({ user: meQuery.data ?? null, loading: meQuery.isPending, sessionEnded, login, logout }),
    [meQuery.data, meQuery.isPending, sessionEnded, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
