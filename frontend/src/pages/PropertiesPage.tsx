import { useSearchParams } from 'react-router'
import { propertiesApi } from '../api/properties'
import { EmptyState } from '../components/EmptyState'
import { Pagination } from '../components/Pagination'
import { PropertyCard } from '../components/PropertyCard'
import { SearchForm } from '../components/SearchForm'
import { readSearch, toSearchParams } from '../searchParams'
import { useApi } from '../useApi'

export function PropertiesPage() {
  const [params, setParams] = useSearchParams()
  const search = readSearch(params)
  const key = toSearchParams(search).toString()

  const { data, error, loading, reload } = useApi((signal) => propertiesApi.search(search, signal), key)

  const hasFilters = Object.entries(search).some(([name, value]) => name !== 'page' && value !== undefined)

  return (
    <div className="container page">
      <h1 className="page__title">Biens disponibles</h1>

      <div className="listing">
        <aside className="listing__filters">
          <SearchForm initial={search} onSearch={(next) => setParams(toSearchParams({ ...next, page: 1 }))} />
        </aside>

        <section className="listing__results" aria-live="polite" aria-busy={loading}>
          {data && (
            <p className="listing__count">
              {data.totalCount === 0
                ? 'Aucun résultat'
                : `${data.totalCount} bien${data.totalCount > 1 ? 's' : ''} trouvé${data.totalCount > 1 ? 's' : ''}`}
            </p>
          )}

          {loading && !data && <p className="page-status" role="status">Chargement des biens…</p>}

          {error && (
            <div className="alert alert--error" role="alert">
              {Object.values(error.fieldErrors).flat().join(' ') || error.message}{' '}
              <button type="button" className="link-button" onClick={reload}>
                Réessayer
              </button>
            </div>
          )}

          {data && data.items.length === 0 && (
            <EmptyState
              title={hasFilters ? 'Aucun bien ne correspond à votre recherche' : 'Aucun bien disponible pour le moment'}
              text={hasFilters ? 'Essayez d’élargir vos critères.' : 'Les annonces apparaîtront ici dès leur publication.'}
            />
          )}

          {data && data.items.length > 0 && (
            <>
              <ul className="property-grid property-grid--two">
                {data.items.map((property) => (
                  <PropertyCard key={property.id} property={property} />
                ))}
              </ul>
              <Pagination
                page={data.page}
                totalPages={data.totalPages}
                onChange={(page) => setParams(toSearchParams({ ...search, page }))}
              />
            </>
          )}
        </section>
      </div>
    </div>
  )
}
