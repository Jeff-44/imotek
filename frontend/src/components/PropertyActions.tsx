import { useState, type FormEvent } from 'react'
import { Link, useLocation } from 'react-router'
import { ApiError } from '../api/client'
import { contactLinks, localInputToIso, requestsApi, type RequestType } from '../api/requests'
import type { Property } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { useApi } from '../useApi'
import { FormField } from './FormField'

const requestLabels: Record<RequestType, string> = {
  Rental: 'Demander une location',
  Purchase: 'Demander à acheter',
  Visit: 'Demander une visite',
}

/**
 * Contact and request buttons of a listing. Only what applies is shown: rental for a property for rent,
 * purchase for one for sale, a visit for both, and nothing once the property is no longer available.
 * The API applies the same rules.
 */
export function PropertyActions({ property }: { property: Property }) {
  const { user } = useAuth()
  const location = useLocation()

  if (property.status !== 'Available') return null
  if (user?.id === property.ownerId) return null

  if (!user) {
    return (
      <section className="actions-card" aria-labelledby="actions-title">
        <h2 id="actions-title" className="actions-card__title">
          Intéressé par ce bien ?
        </h2>
        <p>
          Connectez-vous pour contacter l’agent,{' '}
          {property.transactionType === 'Rent' ? 'demander une location' : 'faire une demande d’achat'} ou demander une
          visite.
        </p>
        <div className="actions-card__buttons">
          <Link to="/connexion" state={{ from: location.pathname }} className="button">
            Se connecter
          </Link>
          <Link to="/inscription" state={{ from: location.pathname }} className="button button--ghost">
            Créer un compte
          </Link>
        </div>
      </section>
    )
  }

  return <SignedInActions property={property} />
}

function SignedInActions({ property }: { property: Property }) {
  const { hasRole } = useAuth()
  const [open, setOpen] = useState<RequestType | null>(null)
  const [sent, setSent] = useState<RequestType | null>(null)
  const types: RequestType[] = [property.transactionType === 'Rent' ? 'Rental' : 'Purchase', 'Visit']

  return (
    <section className="actions-card" aria-labelledby="actions-title">
      <h2 id="actions-title" className="actions-card__title">
        Intéressé par ce bien ?
      </h2>

      <AgentContactCard propertyId={property.id} />

      {sent && (
        <p className="alert alert--success" role="status">
          Votre demande a été envoyée à l’agent. Suivez sa réponse dans <Link to="/mes-demandes">Mes demandes</Link>.
        </p>
      )}

      {hasRole('Client') ? (
        <>
          <div className="actions-card__buttons">
            {types.map((type) => (
              <button
                key={type}
                type="button"
                className={type === 'Visit' ? 'button button--ghost' : 'button'}
                aria-expanded={open === type}
                onClick={() => setOpen(open === type ? null : type)}
              >
                {requestLabels[type]}
              </button>
            ))}
          </div>
          {open && (
            <RequestForm
              key={open}
              propertyId={property.id}
              type={open}
              onSent={(type) => {
                setSent(type)
                // Close only the form that was sent, not one the user opened meanwhile.
                setOpen((current) => (current === type ? null : current))
              }}
              onCancel={() => setOpen(null)}
            />
          )}
        </>
      ) : (
        <p className="field__hint">Les demandes de location, d’achat et de visite sont réservées aux comptes clients.</p>
      )}
    </section>
  )
}

function AgentContactCard({ propertyId }: { propertyId: string }) {
  const { data, error } = useApi((signal) => requestsApi.contact(propertyId, signal), propertyId)

  if (error) return <p className="field__hint">Coordonnées de l’agent indisponibles pour le moment.</p>
  if (!data) return null

  return (
    <div className="contact" data-testid="agent-contact">
      <p className="contact__name">
        Agent : <strong>{data.agentName}</strong>
      </p>
      {data.phoneNumber ? (
        <ContactLinks phone={data.phoneNumber} />
      ) : (
        <p className="field__hint">L’agent n’affiche pas son numéro. Envoyez-lui une demande ci-dessous.</p>
      )}
    </div>
  )
}

export function ContactLinks({ phone }: { phone: string }) {
  const links = contactLinks(phone)
  return (
    <div className="contact__links">
      <a className="button button--ghost" href={links.call}>
        Appeler
      </a>
      <a className="button button--ghost" href={links.whatsApp} target="_blank" rel="noopener noreferrer">
        WhatsApp
      </a>
      <a className="button button--ghost" href={links.sms}>
        SMS
      </a>
      <span className="contact__number">{phone}</span>
    </div>
  )
}

/** Earliest value for the visit picker: the next full hour, in local time. */
function nextHourLocal(): string {
  const date = new Date()
  date.setMinutes(0, 0, 0)
  date.setHours(date.getHours() + 1)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:00`
}

type RequestFormProps = { propertyId: string; type: RequestType; onSent: (type: RequestType) => void; onCancel: () => void }

function RequestForm({ propertyId, type, onSent, onCancel }: RequestFormProps) {
  const { user } = useAuth()
  const [error, setError] = useState<ApiError | null>(null)
  const [pending, setPending] = useState(false)
  const hasPhone = Boolean(user?.phoneNumber)
  const formId = `request-${type.toLowerCase()}`

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const visitAt = String(data.get('visitAt') ?? '')
    setPending(true)
    setError(null)
    try {
      await requestsApi.send(propertyId, {
        type,
        message: String(data.get('message') ?? '').trim() || undefined,
        shareClientPhone: data.get('shareClientPhone') === 'on',
        visitAt: type === 'Visit' && visitAt ? localInputToIso(visitAt) : undefined,
      })
      onSent(type)
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Une erreur est survenue.'))
    } finally {
      setPending(false)
    }
  }

  const errors = error?.fieldErrors ?? {}

  return (
    <form className="request-form" onSubmit={onSubmit} aria-label={requestLabels[type]}>
      {error && (
        <p className="alert alert--error" role="alert">
          {Object.keys(errors).length > 0 ? 'Vérifiez les champs ci-dessous.' : error.message}
        </p>
      )}

      {type === 'Visit' && (
        <FormField id={`${formId}-date`} label="Date et heure souhaitées" errors={errors.visitAt}>
          <input
            id={`${formId}-date`}
            name="visitAt"
            type="datetime-local"
            className="input"
            min={nextHourLocal()}
            required
          />
        </FormField>
      )}

      <FormField id={`${formId}-message`} label="Message (facultatif)" errors={errors.message}>
        <textarea
          id={`${formId}-message`}
          name="message"
          className="input"
          rows={3}
          maxLength={1000}
          placeholder={type === 'Purchase' ? 'Votre budget, vos questions…' : 'Vos disponibilités, vos questions…'}
        />
      </FormField>

      <div className={errors.shareClientPhone ? 'field field--invalid' : 'field'}>
        <label className="checkbox">
          <input type="checkbox" name="shareClientPhone" disabled={!hasPhone} />
          <span>
            {hasPhone
              ? `Partager mon numéro (${user!.phoneNumber}) avec l’agent pour cette demande.`
              : 'Pour partager votre numéro avec l’agent, ajoutez-le d’abord dans '}
            {!hasPhone && <Link to="/compte">Mon compte</Link>}
          </span>
        </label>
        {errors.shareClientPhone?.map((message) => (
          <p key={message} className="field__error">
            {message}
          </p>
        ))}
      </div>

      <div className="request-form__actions">
        <button type="submit" className="button" disabled={pending}>
          {pending ? 'Envoi…' : 'Envoyer la demande'}
        </button>
        <button type="button" className="button button--ghost" onClick={onCancel}>
          Annuler
        </button>
      </div>
    </form>
  )
}
