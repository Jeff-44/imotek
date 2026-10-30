import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { propertiesApi } from '../api/properties'
import { propertyTypeLabels, transactionLabels } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { PropertyImage } from '../components/PropertyImage'
import { StatusBadge } from '../components/StatusBadge'
import { formatPropertyPrice } from '../format'
import { useApi } from '../useApi'

export function PropertyDetailPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const { data: property, error, loading } = useApi((signal) => propertiesApi.get(id, signal), id)
  const [photo, setPhoto] = useState(0)

  if (loading) return <p className="page-status" role="status">Chargement…</p>

  if (error || !property) {
    return (
      <div className="container page">
        <p className="alert alert--error" role="alert">
          {error?.status === 404 ? 'Ce bien n’existe pas ou n’est plus disponible.' : error?.message}
        </p>
        <Link to="/biens">← Retour aux biens disponibles</Link>
      </div>
    )
  }

  const isOwner = user?.id === property.ownerId
  const facts = [
    { label: 'Type', value: propertyTypeLabels[property.propertyType] },
    { label: 'Chambres', value: property.bedrooms },
    { label: 'Salles de bain', value: property.bathrooms },
    { label: 'Superficie', value: property.area ? `${property.area} m²` : null },
  ].filter((fact) => fact.value !== null && fact.value !== undefined)

  return (
    <article className="container page detail">
      <Link to="/biens" className="detail__back">
        ← Retour aux biens disponibles
      </Link>

      <div className="detail__layout">
        <div className="detail__gallery">
          <PropertyImage src={property.imageUrls[photo]} alt={property.title} className="detail__photo" />
          {property.imageUrls.length > 1 && (
            <ul className="detail__thumbs" aria-label="Photos">
              {property.imageUrls.map((url, index) => (
                <li key={url + index}>
                  <button
                    type="button"
                    className={index === photo ? 'detail__thumb detail__thumb--active' : 'detail__thumb'}
                    onClick={() => setPhoto(index)}
                    aria-label={`Photo ${index + 1}`}
                    aria-pressed={index === photo}
                  >
                    <PropertyImage src={url} alt="" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="detail__summary">
          <div className="property-card__tags">
            <span className="tag tag--accent">{transactionLabels[property.transactionType]}</span>
            {(isOwner || property.status !== 'Available') && <StatusBadge status={property.status} />}
          </div>
          <h1 className="detail__title">{property.title}</h1>
          <p className="detail__location">
            {property.address}, {property.city}, {property.department}, {property.country}
          </p>
          <p className="detail__price">{formatPropertyPrice(property)}</p>

          {facts.length > 0 && (
            <dl className="facts">
              {facts.map((fact) => (
                <div key={fact.label} className="facts__item">
                  <dt>{fact.label}</dt>
                  <dd>{fact.value}</dd>
                </div>
              ))}
            </dl>
          )}

          {isOwner && (
            <Link to={`/espace-agent/biens/${property.id}/modifier`} className="button button--ghost">
              Modifier ce bien
            </Link>
          )}
        </div>
      </div>

      <section className="detail__description" aria-labelledby="description-title">
        <h2 id="description-title">Description</h2>
        <p>{property.description}</p>
      </section>
    </article>
  )
}
