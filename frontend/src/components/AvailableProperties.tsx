export type PropertySummary = {
  id: string
  title: string
  location: string
}

type AvailablePropertiesProps = {
  properties: PropertySummary[]
}

export function AvailableProperties({ properties }: AvailablePropertiesProps) {
  return (
    <section id="biens" className="section section--alt" aria-labelledby="properties-title">
      <div className="container">
        <h2 id="properties-title" className="section__title">
          Biens disponibles
        </h2>

        {properties.length === 0 ? (
          <div className="empty-state">
            <svg className="empty-state__icon" viewBox="0 0 48 48" width="48" height="48" aria-hidden="true">
              <path
                d="M8 22 24 9l16 13M12 19v19h24V19"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path d="M20 30h8" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
            </svg>
            <p className="empty-state__title">Aucun bien disponible pour le moment</p>
            <p className="empty-state__text">Les annonces apparaîtront ici dès leur publication.</p>
          </div>
        ) : (
          <ul className="properties">
            {properties.map((property) => (
              <li key={property.id} className="card">
                <h3 className="card__title">{property.title}</h3>
                <p className="card__text">{property.location}</p>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
