import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi, type RegisterInput } from '../api/auth'
import { getToken, setSessionExpiredHandler, setTokens } from '../api/client'
import type { User } from '../api/types'
import { AuthContext, type AuthState } from './context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(() => getToken() !== null)

  const clearSession = useCallback(() => {
    setTokens(null)
    setUser(null)
  }, [])

  const logout = useCallback(() => {
    setUser(null)
    // Revoke the refresh tokens on the server, then forget them locally either way.
    authApi
      .logout()
      .catch(() => undefined)
      .finally(() => setTokens(null))
  }, [])

  useEffect(() => {
    setSessionExpiredHandler(clearSession)
    return () => setSessionExpiredHandler(null)
  }, [clearSession])

  // Restore the session from stored tokens (an expired access token is renewed automatically).
  useEffect(() => {
    if (!getToken()) return
    let active = true
    authApi
      .me()
      .then((me) => active && setUser(me))
      .catch(() => active && clearSession())
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [clearSession])

  const login = useCallback(async (email: string, password: string) => {
    setTokens(await authApi.login(email, password))
    try {
      const me = await authApi.me()
      setUser(me)
      return me
    } catch (error) {
      setTokens(null)
      throw error
    }
  }, [])

  const register = useCallback(
    async (input: RegisterInput) => {
      await authApi.register(input)
      return login(input.email, input.password)
    },
    [login],
  )

  const value = useMemo<AuthState>(
    () => ({
      user,
      loading,
      hasRole: (role) => user?.roles.includes(role) ?? false,
      login,
      register,
      logout,
    }),
    [user, loading, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
