import { Link } from 'react-router'
import { propertiesApi } from '../api/properties'
import { useApi } from '../useApi'
import { EmptyState } from './EmptyState'
import { PropertyCard } from './PropertyCard'

const LATEST_COUNT = 6

/** Latest available properties, straight from the public API. */
export function AvailableProperties() {
  const { data, error, loading, reload } = useApi(
    (signal) => propertiesApi.search({ pageSize: LATEST_COUNT }, signal),
    'latest',
  )

  return (
    <section id="biens" className="section section--alt" aria-labelledby="properties-title">
      <div className="container">
        <div className="section__header">
          <h2 id="properties-title" className="section__title">
            Biens disponibles
          </h2>
          {data && data.totalCount > LATEST_COUNT && (
            <Link to="/biens" className="section__link">
              Voir les {data.totalCount} biens
            </Link>
          )}
        </div>

        {loading && <p className="page-status" role="status">Chargement des biens…</p>}

        {error && (
          <div className="alert alert--error" role="alert">
            {error.message}{' '}
            <button type="button" className="link-button" onClick={reload}>
              Réessayer
            </button>
          </div>
        )}

        {data && data.items.length === 0 && (
          <EmptyState title="Aucun bien disponible pour le moment" text="Les annonces apparaîtront ici dès leur publication." />
        )}

        {data && data.items.length > 0 && (
          <ul className="property-grid">
            {data.items.map((property) => (
              <PropertyCard key={property.id} property={property} />
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
