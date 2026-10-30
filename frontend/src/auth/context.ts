import { createContext } from 'react'
import type { RegisterInput } from '../api/auth'
import type { Role, User } from '../api/types'

export type AuthState = {
  user: User | null
  /** True until the stored token has been checked against the API. */
  loading: boolean
  hasRole: (role: Role) => boolean
  login: (email: string, password: string) => Promise<User>
  register: (input: RegisterInput) => Promise<User>
  logout: () => void
}

export const AuthContext = createContext<AuthState | null>(null)
