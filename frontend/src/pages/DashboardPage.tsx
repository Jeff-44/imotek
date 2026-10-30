import { useState } from 'react'
import { Link } from 'react-router'
import { ApiError } from '../api/client'
import { propertiesApi } from '../api/properties'
import { propertyStatuses, statusLabels, type Property } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { EmptyState } from '../components/EmptyState'
import { PropertyImage } from '../components/PropertyImage'
import { StatusBadge } from '../components/StatusBadge'
import { formatLocation, formatPropertyPrice } from '../format'
import { useApi } from '../useApi'

export function DashboardPage() {
  const { user } = useAuth()
  const { data, error, loading, reload } = useApi((signal) => propertiesApi.mine(1, signal), 'mine')
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState<string | null>(null)

  async function handleDelete(property: Property) {
    if (!window.confirm(`Supprimer définitivement « ${property.title} » ?`)) return
    setDeleting(property.id)
    setDeleteError(null)
    try {
      await propertiesApi.remove(property.id)
      reload()
    } catch (err) {
      setDeleteError(err instanceof ApiError ? err.message : 'La suppression a échoué.')
    } finally {
      setDeleting(null)
    }
  }

  const counts = propertyStatuses.map((status) => ({
    status,
    count: data?.items.filter((p) => p.status === status).length ?? 0,
  }))

  return (
    <div className="container page">
      <div className="page__header">
        <div>
          <h1 className="page__title">Espace agent</h1>
          <p className="page__subtitle">Bonjour {user?.fullName}. Gérez vos biens et leur statut.</p>
        </div>
        <Link to="/espace-agent/biens/nouveau" className="button">
          + Ajouter un bien
        </Link>
      </div>

      {data && data.items.length > 0 && (
        <ul className="stats" aria-label="Répartition par statut">
          {counts.map(({ status, count }) => (
            <li key={status} className="stats__item">
              <span className="stats__value">{count}</span>
              <span className="stats__label">{statusLabels[status]}</span>
            </li>
          ))}
        </ul>
      )}

      {loading && !data && <p className="page-status" role="status">Chargement de vos biens…</p>}

      {(error || deleteError) && (
        <p className="alert alert--error" role="alert">
          {deleteError ?? error?.message}
        </p>
      )}

      {data && data.items.length === 0 && (
        <EmptyState
          title="Vous n’avez encore publié aucun bien"
          text="Ajoutez votre premier bien : il restera en brouillon tant que vous ne le rendez pas disponible."
          action={
            <Link to="/espace-agent/biens/nouveau" className="button">
              Ajouter un bien
            </Link>
          }
        />
      )}

      {data && data.items.length > 0 && (
        <ul className="manage-list">
          {data.items.map((property) => (
            <li key={property.id} className="manage-item">
              <PropertyImage src={property.imageUrls[0]} alt="" className="manage-item__image" />
              <div className="manage-item__body">
                <StatusBadge status={property.status} />
                <h2 className="manage-item__title">
                  <Link to={`/biens/${property.id}`}>{property.title}</Link>
                </h2>
                <p className="manage-item__meta">
                  {formatLocation(property)} · {formatPropertyPrice(property)}
                </p>
              </div>
              <div className="manage-item__actions">
                <Link to={`/espace-agent/biens/${property.id}/modifier`} className="button button--ghost">
                  Modifier
                </Link>
                <button
                  type="button"
                  className="button button--danger"
                  onClick={() => handleDelete(property)}
                  disabled={deleting === property.id}
                >
                  {deleting === property.id ? 'Suppression…' : 'Supprimer'}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
