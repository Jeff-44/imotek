import { apiFetch, toQueryString, type Tokens } from './client'
import type { AgentAccount, AgentStatus, User } from './types'

type TokenResponse = Tokens & { tokenType: string; expiresIn: number }

/** Every account is created as a Client; `requestAgent` files a request an admin must approve. */
export type RegisterInput = {
  email: string
  password: string
  fullName: string
  requestAgent: boolean
  phoneNumber?: string
  phonePublicConsent?: boolean
  agentRequestMessage?: string
}

export type ProfileInput = { fullName: string; phoneNumber: string; phonePublicConsent: boolean }

export type AgentRequestInput = { phoneNumber: string; phonePublicConsent: boolean; message: string }

export const authApi = {
  login: (email: string, password: string) =>
    apiFetch<TokenResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),

  register: (input: RegisterInput) =>
    apiFetch<User>('/api/auth/register', { method: 'POST', body: JSON.stringify(input) }),

  me: () => apiFetch<User>('/api/auth/me'),

  updateMe: (input: ProfileInput) => apiFetch<User>('/api/auth/me', { method: 'PUT', body: JSON.stringify(input) }),

  requestAgent: (input: AgentRequestInput) =>
    apiFetch<User>('/api/auth/agent-request', { method: 'POST', body: JSON.stringify(input) }),

  /**
   * Revokes every refresh token of the user on the server. Fire-and-forget with the token passed
   * explicitly (local tokens are already cleared) and `keepalive`, so it completes during navigation.
   */
  revokeSession: (accessToken: string) => {
    fetch('/api/auth/logout', {
      method: 'POST',
      headers: { Authorization: `Bearer ${accessToken}` },
      keepalive: true,
    }).catch(() => undefined)
  },
}

/** Admin review of agent requests. `None` lists legacy agents (Agent role without approval). */
export const agentRequestsApi = {
  list: (status: AgentStatus | 'All', signal?: AbortSignal) =>
    apiFetch<AgentAccount[]>(`/api/admin/agent-requests${toQueryString({ status })}`, { signal }),

  approve: (userId: string) =>
    apiFetch<AgentAccount>(`/api/admin/agent-requests/${encodeURIComponent(userId)}/approve`, { method: 'POST' }),

  reject: (userId: string, reason: string) =>
    apiFetch<AgentAccount>(`/api/admin/agent-requests/${encodeURIComponent(userId)}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
}
