import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi, type RegisterInput } from '../api/auth'
import { getToken, setToken, setUnauthorizedHandler } from '../api/client'
import type { User } from '../api/types'
import { AuthContext, type AuthState } from './context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(() => getToken() !== null)

  const logout = useCallback(() => {
    setToken(null)
    setUser(null)
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    return () => setUnauthorizedHandler(null)
  }, [logout])

  // Restore the session from a stored token.
  useEffect(() => {
    if (!getToken()) return
    let active = true
    authApi
      .me()
      .then((me) => active && setUser(me))
      .catch(() => active && logout())
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [logout])

  const login = useCallback(async (email: string, password: string) => {
    const token = await authApi.login(email, password)
    setToken(token.accessToken)
    try {
      const me = await authApi.me()
      setUser(me)
      return me
    } catch (error) {
      setToken(null)
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
