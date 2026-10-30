import { apiFetch } from './client'
import type { User } from './types'

type TokenResponse = { tokenType: string; accessToken: string; expiresIn: number }

export type RegisterInput = {
  email: string
  password: string
  fullName: string
  role: 'Agent' | 'Client'
}

export const authApi = {
  login: (email: string, password: string) =>
    apiFetch<TokenResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),

  register: (input: RegisterInput) =>
    apiFetch<User>('/api/auth/register', { method: 'POST', body: JSON.stringify(input) }),

  me: () => apiFetch<User>('/api/auth/me'),
}
