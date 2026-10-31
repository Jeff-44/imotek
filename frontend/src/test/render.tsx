import { cleanup, render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router'
import { afterEach, vi } from 'vitest'
import type { User } from '../api/types'
import { AuthContext, type AuthState } from '../auth/context'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

export function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 'u1',
    email: 'user@imotek.test',
    fullName: 'Marie Joseph',
    roles: ['Client'],
    phoneNumber: null,
    phonePublicConsent: false,
    agentStatus: 'None',
    agentRequestedAt: null,
    agentRejectionReason: null,
    canPublish: false,
    isLegacyAgent: false,
    ...overrides,
  }
}

export function authState(user: User | null, overrides: Partial<AuthState> = {}): AuthState {
  return {
    user,
    loading: false,
    hasRole: (role) => user?.roles.includes(role) ?? false,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    updateUser: vi.fn(),
    ...overrides,
  }
}

/** Renders inside a router and a controlled auth context (no network, no storage). */
export function renderWithAuth(ui: ReactNode, auth: AuthState, route = '/') {
  return render(
    <AuthContext.Provider value={auth}>
      <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
    </AuthContext.Provider>,
  )
}

type Handler = (url: string, init: RequestInit) => Response | Promise<Response>

/** Replaces fetch with `handler` and records every call. */
export function mockFetch(handler: Handler) {
  const calls: { url: string; init: RequestInit }[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init: RequestInit = {}) => {
      calls.push({ url, init })
      return handler(url, init)
    }),
  )
  return calls
}

export const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
