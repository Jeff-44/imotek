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
  // Display only: the API refuses content changes from unapproved agents anyway.
  const canPublish = user?.canPublish ?? false
  const { data, error, loading, reload } = useApi((signal) => propertiesApi.mine(1, signal), 'mine')
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState<string | null>(null)

  async function handleArchive(property: Property) {
    if (!window.confirm(`Archiver « ${property.title} » ? L’annonce ne sera plus visible ; elle reste dans votre historique avec ses demandes.`)) return
    setDeleting(property.id)
    setDeleteError(null)
    try {
      await propertiesApi.remove(property.id)
      reload()
    } catch (err) {
      setDeleteError(err instanceof ApiError ? err.message : 'L’archivage a échoué.')
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
          <p className="page__subtitle">Bonjour {user?.fullName}. Gérez vos biens, leur statut et les demandes des clients.</p>
        </div>
        <div className="page__actions">
          <Link to="/espace-agent/demandes" className="button button--ghost">
            Demandes reçues
          </Link>
          {canPublish && (
            <Link to="/espace-agent/biens/nouveau" className="button">
              + Ajouter un bien
            </Link>
          )}
        </div>
      </div>

      {!canPublish && (
        <p className="alert alert--warning" role="status" data-testid="approval-required">
          {user?.isLegacyAgent
            ? 'Compte agent existant en attente de validation par un administrateur. Vous pouvez consulter et retirer vos annonces, mais pas en publier ni en modifier. '
            : 'Votre compte agent doit être validé par un administrateur avant de publier. '}
          <Link to="/compte">Voir mon compte</Link>
        </p>
      )}

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
            canPublish && (
              <Link to="/espace-agent/biens/nouveau" className="button">
                Ajouter un bien
              </Link>
            )
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
                {canPublish && property.status !== 'Archived' && (
                  <Link to={`/espace-agent/biens/${property.id}/modifier`} className="button button--ghost">
                    Modifier
                  </Link>
                )}
                {property.status !== 'Archived' && (
                  <button
                    type="button"
                    className="button button--danger"
                    onClick={() => handleArchive(property)}
                    disabled={deleting === property.id}
                  >
                    {deleting === property.id ? 'Archivage…' : 'Archiver'}
                  </button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
