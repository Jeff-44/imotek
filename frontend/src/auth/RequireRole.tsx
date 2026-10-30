import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import type { Role } from '../api/types'
import { useAuth } from './useAuth'

/** Client-side guard for navigation only; the API enforces the same rule. */
export function RequireRole({ role, children }: { role: Role; children: ReactNode }) {
  const { user, loading, hasRole } = useAuth()
  const location = useLocation()

  if (loading) return <p className="page-status" role="status">Chargement…</p>
  if (!user) return <Navigate to="/connexion" replace state={{ from: location.pathname }} />
  if (!hasRole(role)) {
    return (
      <div className="container page">
        <p className="alert alert--error">Cet espace est réservé aux agents et propriétaires.</p>
      </div>
    )
  }
  return children
}
