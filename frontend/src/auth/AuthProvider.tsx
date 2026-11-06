import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi, type RegisterInput } from '../api/auth'
import { ApiError, getToken, setSessionExpiredHandler, setTokens } from '../api/client'
import type { User } from '../api/types'
import { AuthContext, type AuthState } from './context'

const RESTORE_RETRIES = 3
const RESTORE_RETRY_SECONDS = 3
const MAX_RESTORE_WAIT_SECONDS = 30

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(() => getToken() !== null)

  const clearSession = useCallback(() => {
    setTokens(null)
    setUser(null)
  }, [])

  const logout = useCallback(() => {
    // Forget the tokens first, so a reload right after logging out cannot restore the session;
    // the server-side revocation uses the token captured here and survives page navigation.
    const accessToken = getToken()
    setTokens(null)
    setUser(null)
    if (accessToken) authApi.revokeSession(accessToken)
  }, [])

  useEffect(() => {
    setSessionExpiredHandler(clearSession)
    return () => setSessionExpiredHandler(null)
  }, [clearSession])

  // Restore the session from stored tokens (an expired access token is renewed automatically).
  // A temporary failure (rate limit, server error, no network) keeps the tokens and retries a
  // few times, waiting as long as Retry-After asks; only a refused session signs the user out.
  useEffect(() => {
    if (!getToken()) return
    let active = true
    let timer: number | undefined

    const attempt = (remaining: number) => {
      authApi
        .me()
        .then((me) => {
          if (!active) return
          setUser(me)
          setLoading(false)
        })
        .catch((error: unknown) => {
          if (!active) return
          const temporary = error instanceof ApiError && error.isTemporary
          if (temporary && remaining > 0) {
            const delay = Math.min(error.retryAfter ?? RESTORE_RETRY_SECONDS, MAX_RESTORE_WAIT_SECONDS) * 1000
            timer = window.setTimeout(() => attempt(remaining - 1), delay)
            return
          }
          if (!temporary) clearSession()
          setLoading(false)
        })
    }

    attempt(RESTORE_RETRIES)
    return () => {
      active = false
      window.clearTimeout(timer)
    }
  }, [clearSession])

  const login = useCallback(async (email: string, password: string) => {
    // Signing in replaces any previous session: a stale token must not be sent with the credentials.
    setTokens(null)
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
      setTokens(null) // a new account never inherits a previous, possibly stale, session
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
      updateUser: setUser,
    }),
    [user, loading, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
