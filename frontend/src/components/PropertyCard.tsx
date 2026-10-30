import { Link } from 'react-router'
import { propertyTypeLabels, transactionLabels, type Property } from '../api/types'
import { formatLocation, formatPropertyPrice } from '../format'
import { PropertyImage } from './PropertyImage'

export function PropertyCard({ property }: { property: Property }) {
  return (
    <li className="property-card">
      <Link to={`/biens/${property.id}`} className="property-card__link">
        <PropertyImage src={property.imageUrls[0]} alt={property.title} className="property-card__image" />
        <div className="property-card__body">
          <div className="property-card__tags">
            <span className="tag tag--accent">{transactionLabels[property.transactionType]}</span>
            <span className="tag">{propertyTypeLabels[property.propertyType]}</span>
          </div>
          <h3 className="property-card__title">{property.title}</h3>
          <p className="property-card__location">{formatLocation(property)}</p>
          <p className="property-card__price">{formatPropertyPrice(property)}</p>
        </div>
      </Link>
    </li>
  )
}
