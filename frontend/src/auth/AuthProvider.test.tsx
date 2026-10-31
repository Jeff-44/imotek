// @vitest-environment jsdom
import { act, render, waitFor } from '@testing-library/react'
import { useContext, useEffect } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getToken, setTokens } from '../api/client'
import { json, makeUser, mockFetch } from '../test/render'
import { AuthProvider } from './AuthProvider'
import { AuthContext, type AuthState } from './context'

function memoryStorage(): Storage {
  const data = new Map<string, string>()
  return {
    get length() {
      return data.size
    },
    clear: () => data.clear(),
    getItem: (key) => data.get(key) ?? null,
    key: (index) => [...data.keys()][index] ?? null,
    removeItem: (key) => void data.delete(key),
    setItem: (key, value) => void data.set(key, value),
  }
}

describe('logout', () => {
  beforeEach(() => vi.stubGlobal('localStorage', memoryStorage()))

  it('forgets the tokens immediately and revokes the session with the captured token', async () => {
    setTokens({ accessToken: 'a1', refreshToken: 'r1' })
    const calls = mockFetch((url) => (url === '/api/auth/me' ? json(200, makeUser()) : json(204)))
    let auth: AuthState | null = null
    const capture = (value: AuthState | null) => {
      auth = value
    }
    function Probe({ onAuth }: { onAuth: (value: AuthState | null) => void }) {
      const value = useContext(AuthContext)
      useEffect(() => onAuth(value), [value, onAuth])
      return null
    }
    render(
      <AuthProvider>
        <Probe onAuth={capture} />
      </AuthProvider>,
    )
    await waitFor(() => expect(auth?.user?.email).toBe('user@imotek.test'))

    act(() => auth!.logout())

    // Cleared synchronously: a reload at this point cannot restore the session.
    expect(getToken()).toBeNull()
    expect(auth!.user).toBeNull()
    const revoke = calls.find((c) => c.url === '/api/auth/logout')
    expect(revoke?.init.keepalive).toBe(true)
    expect(new Headers(revoke?.init.headers).get('Authorization')).toBe('Bearer a1')
  })
})

describe('session restore', () => {
  beforeEach(() => vi.stubGlobal('localStorage', memoryStorage()))

  function Probe({ onAuth }: { onAuth: (value: AuthState | null) => void }) {
    const value = useContext(AuthContext)
    useEffect(() => onAuth(value), [value, onAuth])
    return null
  }

  it('retries after a temporary failure instead of signing the user out', async () => {
    setTokens({ accessToken: 'a1', refreshToken: 'r1' })
    let meCalls = 0
    mockFetch(() => (++meCalls === 1 ? new Response(null, { status: 503, headers: { 'Retry-After': '0' } }) : json(200, makeUser())))
    let auth: AuthState | null = null
    render(
      <AuthProvider>
        <Probe
          onAuth={(value) => {
            auth = value
          }}
        />
      </AuthProvider>,
    )

    await waitFor(() => expect(auth?.user?.email).toBe('user@imotek.test'))
    expect(meCalls).toBe(2)
    expect(getToken()).toBe('a1')
  })

  it('signs out when the session is refused', async () => {
    setTokens({ accessToken: 'a1', refreshToken: 'revoked' })
    mockFetch(() => json(401))
    let auth: AuthState | null = null
    render(
      <AuthProvider>
        <Probe
          onAuth={(value) => {
            auth = value
          }}
        />
      </AuthProvider>,
    )

    await waitFor(() => expect(auth?.loading).toBe(false))
    expect(auth!.user).toBeNull()
    expect(getToken()).toBeNull()
  })
})
