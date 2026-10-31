import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import type { Role } from '../api/types'
import { useAuth } from './useAuth'

const deniedMessages: Record<Role, string> = {
  Agent: 'Cet espace est réservé aux agents et propriétaires.',
  Admin: 'Cet espace est réservé aux administrateurs.',
  Client: 'Cet espace est réservé aux clients.',
}

/**
 * Client-side guard for navigation only; the API enforces the same rules.
 * Without `role`, any signed-in user is accepted.
 */
export function RequireRole({ role, children }: { role?: Role; children: ReactNode }) {
  const { user, loading, hasRole } = useAuth()
  const location = useLocation()

  if (loading) return <p className="page-status" role="status">Chargement…</p>
  if (!user) return <Navigate to="/connexion" replace state={{ from: location.pathname }} />
  if (role && !hasRole(role)) {
    return (
      <div className="container page">
        <p className="alert alert--error">{deniedMessages[role]}</p>
      </div>
    )
  }
  return children
}
