import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { propertiesApi } from '../api/properties'
import {
  currencies,
  departments,
  propertyStatuses,
  propertyTypeLabels,
  propertyTypes,
  statusLabels,
  transactionLabels,
  transactionTypes,
  type Property,
  type PropertyInput,
} from '../api/types'
import { FormField } from '../components/FormField'
import { useApi } from '../useApi'

const MAX_IMAGES = 10

export function PropertyFormPage() {
  const { id } = useParams()
  const { data, error, loading } = useApi(
    (signal) => (id ? propertiesApi.get(id, signal) : Promise.resolve(null)),
    id ?? 'new',
  )

  if (id && loading) return <p className="page-status" role="status">Chargement…</p>
  if (id && (error || !data)) {
    return (
      <div className="container page">
        <p className="alert alert--error" role="alert">
          {error?.status === 404 ? 'Ce bien est introuvable.' : error?.message}
        </p>
        <Link to="/espace-agent">← Retour à mon espace</Link>
      </div>
    )
  }

  return <PropertyForm key={id ?? 'new'} property={data} />
}

function numberOrNull(value: FormDataEntryValue | null): number | null {
  if (value === null || String(value).trim() === '') return null
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function PropertyForm({ property }: { property: Property | null }) {
  const navigate = useNavigate()
  const [images, setImages] = useState<string[]>(property?.imageUrls.length ? property.imageUrls : [''])
  const [error, setError] = useState<ApiError | null>(null)
  const [pending, setPending] = useState(false)
  const errors = error?.fieldErrors ?? {}
  const isEdit = property !== null

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const text = (name: string) => String(data.get(name) ?? '').trim()

    const input: PropertyInput = {
      title: text('title'),
      description: text('description'),
      propertyType: text('propertyType') as PropertyInput['propertyType'],
      transactionType: text('transactionType') as PropertyInput['transactionType'],
      price: numberOrNull(data.get('price')) ?? 0,
      currency: text('currency') as PropertyInput['currency'],
      country: 'Haïti',
      department: text('department'),
      city: text('city'),
      address: text('address'),
      bedrooms: numberOrNull(data.get('bedrooms')),
      bathrooms: numberOrNull(data.get('bathrooms')),
      area: numberOrNull(data.get('area')),
      status: text('status') as PropertyInput['status'],
      imageUrls: images.map((url) => url.trim()).filter(Boolean),
    }

    setPending(true)
    setError(null)
    try {
      if (isEdit) await propertiesApi.update(property.id, input)
      else await propertiesApi.create(input)
      navigate('/espace-agent')
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Une erreur est survenue.'))
      window.scrollTo(0, 0)
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="container page">
      <Link to="/espace-agent" className="detail__back">
        ← Retour à mon espace
      </Link>
      <h1 className="page__title">{isEdit ? 'Modifier le bien' : 'Ajouter un bien'}</h1>

      {error && (
        <p className="alert alert--error" role="alert">
          {error.message}
        </p>
      )}

      <form className="card property-form" onSubmit={handleSubmit} noValidate>
        <fieldset className="property-form__section">
          <legend>Annonce</legend>
          <FormField id="title" label="Titre" errors={errors.title}>
            <input id="title" name="title" className="input" required maxLength={150} defaultValue={property?.title} />
          </FormField>
          <FormField id="description" label="Description" errors={errors.description}>
            <textarea
              id="description"
              name="description"
              className="input"
              rows={5}
              required
              maxLength={4000}
              defaultValue={property?.description}
            />
          </FormField>
          <div className="property-form__grid">
            <FormField id="propertyType" label="Type de bien" errors={errors.propertyType}>
              <select id="propertyType" name="propertyType" className="input" defaultValue={property?.propertyType ?? 'House'}>
                {propertyTypes.map((type) => (
                  <option key={type} value={type}>
                    {propertyTypeLabels[type]}
                  </option>
                ))}
              </select>
            </FormField>
            <FormField id="transactionType" label="Transaction" errors={errors.transactionType}>
              <select
                id="transactionType"
                name="transactionType"
                className="input"
                defaultValue={property?.transactionType ?? 'Rent'}
              >
                {transactionTypes.map((type) => (
                  <option key={type} value={type}>
                    {transactionLabels[type]}
                  </option>
                ))}
              </select>
            </FormField>
            <FormField
              id="status"
              label="Statut"
              errors={errors.status}
              hint="Seuls les biens « Disponible » apparaissent dans la recherche publique."
            >
              <select id="status" name="status" className="input" defaultValue={property?.status ?? 'Draft'}>
                {propertyStatuses.map((status) => (
                  <option key={status} value={status}>
                    {statusLabels[status]}
                  </option>
                ))}
              </select>
            </FormField>
          </div>
        </fieldset>

        <fieldset className="property-form__section">
          <legend>Prix</legend>
          <div className="property-form__grid">
            <FormField id="price" label="Prix" errors={errors.price} hint="Par mois pour une location.">
              <input
                id="price"
                name="price"
                type="number"
                min="1"
                step="any"
                inputMode="decimal"
                className="input"
                required
                defaultValue={property?.price}
              />
            </FormField>
            <FormField id="currency" label="Devise" errors={errors.currency}>
              <select id="currency" name="currency" className="input" defaultValue={property?.currency ?? 'HTG'}>
                {currencies.map((currency) => (
                  <option key={currency} value={currency}>
                    {currency}
                  </option>
                ))}
              </select>
            </FormField>
          </div>
        </fieldset>

        <fieldset className="property-form__section">
          <legend>Localisation</legend>
          <div className="property-form__grid">
            <FormField id="department" label="Département" errors={errors.department}>
              <select id="department" name="department" className="input" required defaultValue={property?.department ?? ''}>
                <option value="" disabled>
                  Choisir…
                </option>
                {departments.map((department) => (
                  <option key={department} value={department}>
                    {department}
                  </option>
                ))}
              </select>
            </FormField>
            <FormField id="city" label="Commune / ville" errors={errors.city}>
              <input id="city" name="city" className="input" required maxLength={100} defaultValue={property?.city} />
            </FormField>
          </div>
          <FormField id="address" label="Adresse" errors={errors.address}>
            <input id="address" name="address" className="input" required maxLength={250} defaultValue={property?.address} />
          </FormField>
        </fieldset>

        <fieldset className="property-form__section">
          <legend>Caractéristiques (facultatif)</legend>
          <div className="property-form__grid">
            <FormField id="bedrooms" label="Chambres" errors={errors.bedrooms}>
              <input id="bedrooms" name="bedrooms" type="number" min="0" max="100" className="input" defaultValue={property?.bedrooms ?? ''} />
            </FormField>
            <FormField id="bathrooms" label="Salles de bain" errors={errors.bathrooms}>
              <input id="bathrooms" name="bathrooms" type="number" min="0" max="100" className="input" defaultValue={property?.bathrooms ?? ''} />
            </FormField>
            <FormField id="area" label="Superficie (m²)" errors={errors.area}>
              <input id="area" name="area" type="number" min="1" step="any" className="input" defaultValue={property?.area ?? ''} />
            </FormField>
          </div>
        </fieldset>

        <fieldset className="property-form__section">
          <legend>Photos</legend>
          <p className="field__hint">Adresses (URL https) de vos photos, {MAX_IMAGES} au maximum. La première sert de photo principale.</p>
          {errors.imageUrls?.map((message) => (
            <p key={message} className="field__error">
              {message}
            </p>
          ))}
          <ul className="image-inputs">
            {images.map((url, index) => (
              <li key={index} className="image-inputs__row">
                <input
                  type="url"
                  className="input"
                  placeholder="https://…"
                  aria-label={`Photo ${index + 1}`}
                  value={url}
                  onChange={(event) => setImages((list) => list.map((item, i) => (i === index ? event.target.value : item)))}
                />
                <button
                  type="button"
                  className="button button--ghost"
                  aria-label={`Retirer la photo ${index + 1}`}
                  onClick={() => setImages((list) => (list.length > 1 ? list.filter((_, i) => i !== index) : ['']))}
                >
                  Retirer
                </button>
              </li>
            ))}
          </ul>
          {images.length < MAX_IMAGES && (
            <button type="button" className="link-button" onClick={() => setImages((list) => [...list, ''])}>
              + Ajouter une photo
            </button>
          )}
        </fieldset>

        <div className="property-form__actions">
          <Link to="/espace-agent" className="button button--ghost">
            Annuler
          </Link>
          <button type="submit" className="button" disabled={pending}>
            {pending ? 'Enregistrement…' : isEdit ? 'Enregistrer' : 'Publier le bien'}
          </button>
        </div>
      </form>
    </div>
  )
}
