import { useState, type FormEvent } from 'react'
import { Link, useLocation } from 'react-router'
import { ApiError } from '../api/client'
import { contactLinks, localDateTimeToIso, requestsApi, trackingPath, type RequestType } from '../api/requests'
import type { Property } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { useApi } from '../useApi'
import { FormField } from './FormField'

const requestLabels: Record<RequestType, string> = {
  Rental: 'Demander une location',
  Purchase: 'Demander à acheter',
  Visit: 'Demander une visite',
}

type Sent = { type: RequestType; trackingToken: string | null }

/**
 * Contact and request buttons of a listing, open to visitors without an account: the agent's number
 * (when they agreed to show it), a visit for any listing, a rental for one for rent. A purchase request
 * needs an account. Nothing is shown to the owner, nor once the property is no longer available.
 * The API applies the same rules.
 */
export function PropertyActions({ property }: { property: Property }) {
  const { user, hasRole, loading } = useAuth()
  const location = useLocation()
  const [open, setOpen] = useState<RequestType | null>(null)
  const [sent, setSent] = useState<Sent | null>(null)

  // While the session is being restored, who the user is is not known yet: show no action at all
  // rather than a visitor's buttons to a client (or a client's to a visitor).
  if (loading) return null
  if (property.status !== 'Available') return null
  if (user?.id === property.ownerId) return null

  const canRequest = !user || hasRole('Client')
  const types: RequestType[] = [property.transactionType === 'Rent' ? 'Rental' : 'Purchase', 'Visit']
  // Purchases need an account: a visitor is offered to sign in instead.
  const purchaseNeedsAccount = !user && property.transactionType === 'Sale'

  return (
    <section className="actions-card" aria-labelledby="actions-title">
      <h2 id="actions-title" className="actions-card__title">
        Intéressé par ce bien ?
      </h2>

      <AgentContactCard propertyId={property.id} />

      {sent && <SentConfirmation sent={sent} signedIn={Boolean(user)} />}

      {canRequest ? (
        <>
          <div className="actions-card__buttons">
            {types.map((type) =>
              type === 'Purchase' && purchaseNeedsAccount ? (
                <Link key={type} to="/connexion" state={{ from: location.pathname }} className="button">
                  Se connecter pour demander à acheter
                </Link>
              ) : (
                <button
                  key={type}
                  type="button"
                  className={type === 'Visit' ? 'button button--ghost' : 'button'}
                  aria-expanded={open === type}
                  onClick={() => setOpen(open === type ? null : type)}
                >
                  {requestLabels[type]}
                </button>
              ),
            )}
          </div>
          {open && (
            <RequestForm
              key={open}
              propertyId={property.id}
              type={open}
              onSent={(result) => {
                setSent(result)
                // Close only the form that was sent, not one the user opened meanwhile.
                setOpen((current) => (current === result.type ? null : current))
              }}
              onCancel={() => setOpen(null)}
            />
          )}
          {!user && (
            <p className="field__hint">
              Pas besoin de compte pour une visite ou une location.{' '}
              <Link to="/inscription" state={{ from: location.pathname }}>
                Créer un compte
              </Link>{' '}
              permet de retrouver toutes vos demandes.
            </p>
          )}
        </>
      ) : (
        <p className="field__hint">Les demandes de location, d’achat et de visite sont réservées aux comptes clients.</p>
      )}
    </section>
  )
}

function SentConfirmation({ sent, signedIn }: { sent: Sent; signedIn: boolean }) {
  const link = sent.trackingToken ? trackingPath(sent.trackingToken) : null
  return (
    <div className="alert alert--success" role="status">
      <p>Votre demande a été envoyée à l’agent.</p>
      {signedIn && (
        <p>
          Suivez sa réponse dans <Link to="/mes-demandes">Mes demandes</Link>.
        </p>
      )}
      {link && (
        <p>
          L’agent vous contactera par téléphone ou par e-mail. Pour suivre sa réponse, conservez ce lien personnel :{' '}
          <Link to={link} data-testid="tracking-link">
            {window.location.origin}
            {link}
          </Link>
        </p>
      )}
    </div>
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

const pad = (n: number) => String(n).padStart(2, '0')
const localDate = (date: Date) => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`

const numberOrUndefined = (value: FormDataEntryValue | null) => {
  const text = String(value ?? '').trim()
  return text === '' ? undefined : Number(text)
}

type RequestFormProps = {
  propertyId: string
  type: RequestType
  onSent: (sent: Sent) => void
  onCancel: () => void
}

function RequestForm({ propertyId, type, onSent, onCancel }: RequestFormProps) {
  const { user } = useAuth()
  const [error, setError] = useState<ApiError | null>(null)
  const [pending, setPending] = useState(false)
  const guest = !user
  const hasPhone = Boolean(user?.phoneNumber)
  const formId = `request-${type.toLowerCase()}`
  const [today] = useState(() => localDate(new Date()))

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const text = (name: string) => String(data.get(name) ?? '').trim()
    setPending(true)
    setError(null)
    try {
      const created = await requestsApi.send(propertyId, {
        type,
        message: text('message') || undefined,
        shareClientPhone: guest || data.get('shareClientPhone') === 'on',
        visitAt: type === 'Visit' && text('visitDate') && text('visitTime') ? localDateTimeToIso(text('visitDate'), text('visitTime')) : undefined,
        fullName: guest ? text('fullName') : undefined,
        email: guest ? text('email') : undefined,
        phoneNumber: guest ? text('phoneNumber') : undefined,
        currentAddress: type === 'Rental' ? text('currentAddress') || undefined : undefined,
        durationMonths: type === 'Rental' ? numberOrUndefined(data.get('durationMonths')) : undefined,
        moveInDate: type === 'Rental' ? text('moveInDate') || undefined : undefined,
        occupants: type === 'Rental' ? numberOrUndefined(data.get('occupants')) : undefined,
      })
      onSent({ type, trackingToken: created.trackingToken ?? null })
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Une erreur est survenue.'))
    } finally {
      setPending(false)
    }
  }

  const errors = error?.fieldErrors ?? {}
  const id = (name: string) => `${formId}-${name}`

  return (
    <form className="request-form" onSubmit={onSubmit} aria-label={requestLabels[type]}>
      {error && (
        <p className="alert alert--error" role="alert">
          {Object.keys(errors).length > 0 ? 'Vérifiez les champs ci-dessous.' : error.message}
        </p>
      )}

      {guest && (
        <>
          <FormField id={id('name')} label="Nom complet" errors={errors.fullName}>
            <input id={id('name')} name="fullName" className="input" autoComplete="name" required maxLength={150} />
          </FormField>
          <div className="request-form__row">
            <FormField id={id('phone')} label="Numéro de téléphone" errors={errors.phoneNumber} hint="Par exemple +509 3712 3456.">
              <input id={id('phone')} name="phoneNumber" type="tel" className="input" autoComplete="tel" inputMode="tel" required />
            </FormField>
            <FormField id={id('email')} label="Adresse e-mail" errors={errors.email}>
              <input id={id('email')} name="email" type="email" className="input" autoComplete="email" required maxLength={256} />
            </FormField>
          </div>
        </>
      )}

      {type === 'Visit' && (
        <div className="request-form__row">
          <FormField id={id('date')} label="Date souhaitée" errors={errors.visitAt}>
            <input id={id('date')} name="visitDate" type="date" className="input" min={today} required />
          </FormField>
          <FormField id={id('time')} label="Heure souhaitée">
            <input id={id('time')} name="visitTime" type="time" className="input" step={900} required />
          </FormField>
        </div>
      )}

      {type === 'Rental' && (
        <>
          <div className="request-form__row">
            <FormField id={id('move-in')} label="Date d’entrée souhaitée" errors={errors.moveInDate}>
              <input id={id('move-in')} name="moveInDate" type="date" className="input" min={today} required />
            </FormField>
            <FormField id={id('duration')} label="Durée souhaitée (mois)" errors={errors.durationMonths}>
              <input id={id('duration')} name="durationMonths" type="number" className="input" min={1} max={120} required />
            </FormField>
          </div>
          <div className="request-form__row">
            <FormField id={id('occupants')} label="Nombre de personnes (facultatif)" errors={errors.occupants}>
              <input id={id('occupants')} name="occupants" type="number" className="input" min={1} max={50} />
            </FormField>
            <FormField id={id('address')} label="Adresse actuelle (facultatif)" errors={errors.currentAddress}>
              <input id={id('address')} name="currentAddress" className="input" autoComplete="street-address" maxLength={250} />
            </FormField>
          </div>
        </>
      )}

      <FormField id={id('message')} label="Message pour l’agent (facultatif)" errors={errors.message}>
        <textarea
          id={id('message')}
          name="message"
          className="input"
          rows={3}
          maxLength={1000}
          placeholder={type === 'Purchase' ? 'Votre budget, vos questions…' : 'Vos disponibilités, vos questions…'}
        />
      </FormField>

      {guest ? (
        <p className="field__hint">Vos coordonnées sont transmises uniquement à l’agent de ce bien, pour vous répondre.</p>
      ) : (
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
      )}

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
