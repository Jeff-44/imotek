import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation } from 'react-router'
import { ApiError } from '../api/client'
import type { User } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { canOpen, defaultPageFor } from '../auth/redirects'
import { FormField } from '../components/FormField'

/** Once signed in, go back where the user came from, or to the page that fits their role. */
function SignedInRedirect({ user }: { user: User }) {
  const from = (useLocation().state as { from?: string } | null)?.from
  // "from" may have been recorded for the previous user of this browser (signed out on a
  // protected page): only go back there if this user may open it.
  return <Navigate to={from && canOpen(from, user) ? from : defaultPageFor(user)} replace />
}

function useSubmit(action: (data: FormData) => Promise<void>) {
  const [error, setError] = useState<ApiError | null>(null)
  const [pending, setPending] = useState(false)

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPending(true)
    setError(null)
    try {
      await action(new FormData(event.currentTarget))
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Une erreur est survenue.'))
    } finally {
      setPending(false)
    }
  }

  return { error, pending, onSubmit }
}

const field = (data: FormData, name: string) => String(data.get(name) ?? '')

export function LoginPage() {
  const { user, login } = useAuth()
  const { error, pending, onSubmit } = useSubmit(async (data) => {
    await login(field(data, 'email'), field(data, 'password'))
  })

  if (user) return <SignedInRedirect user={user} />

  return (
    <div className="container page auth">
      <form className="card auth__card" onSubmit={onSubmit} noValidate>
        <h1 className="page__title">Connexion</h1>
        {error && (
          <p className="alert alert--error" role="alert">
            {error.status === 401 ? 'Adresse e-mail ou mot de passe incorrect.' : error.message}
          </p>
        )}
        <FormField id="login-email" label="Adresse e-mail">
          <input id="login-email" name="email" type="email" className="input" autoComplete="email" required />
        </FormField>
        <FormField id="login-password" label="Mot de passe">
          <input id="login-password" name="password" type="password" className="input" autoComplete="current-password" required />
        </FormField>
        <button type="submit" className="button" disabled={pending}>
          {pending ? 'Connexion…' : 'Se connecter'}
        </button>
        <p className="auth__switch">
          Pas encore de compte ? <Link to="/inscription">Créer un compte</Link>
        </p>
      </form>
    </div>
  )
}

export function RegisterPage() {
  const { user, register } = useAuth()
  const [requestAgent, setRequestAgent] = useState(false)
  const { error, pending, onSubmit } = useSubmit(async (data) => {
    const phone = field(data, 'phoneNumber').trim()
    await register({
      email: field(data, 'email'),
      password: field(data, 'password'),
      fullName: field(data, 'fullName'),
      requestAgent,
      phoneNumber: phone || undefined,
      phonePublicConsent: requestAgent && data.get('phonePublicConsent') === 'on',
      agentRequestMessage: requestAgent ? field(data, 'agentRequestMessage').trim() || undefined : undefined,
    })
  })

  if (user) return <SignedInRedirect user={user} />

  const errors = error?.fieldErrors ?? {}

  return (
    <div className="container page auth">
      <form className="card auth__card" onSubmit={onSubmit} noValidate>
        <h1 className="page__title">Créer un compte</h1>
        {error && (
          <p className="alert alert--error" role="alert">
            {error.status === 409 ? 'Un compte existe déjà avec cette adresse e-mail.' : error.message}
          </p>
        )}

        <fieldset className="field">
          <legend className="field__label">Je souhaite</legend>
          <div className="segmented">
            <label className="segmented__option">
              <input type="radio" name="accountType" value="Client" checked={!requestAgent} onChange={() => setRequestAgent(false)} />
              <span>Chercher un bien</span>
            </label>
            <label className="segmented__option">
              <input type="radio" name="accountType" value="Agent" checked={requestAgent} onChange={() => setRequestAgent(true)} />
              <span>Publier des annonces</span>
            </label>
          </div>
        </fieldset>

        {requestAgent && (
          <p className="alert alert--info">
            Votre compte sera créé immédiatement. La publication d’annonces sera possible après validation de votre
            demande par un administrateur.
          </p>
        )}

        <FormField id="register-name" label="Nom complet" errors={errors.fullName}>
          <input id="register-name" name="fullName" className="input" autoComplete="name" required maxLength={150} />
        </FormField>
        <FormField id="register-email" label="Adresse e-mail" errors={errors.email}>
          <input id="register-email" name="email" type="email" className="input" autoComplete="email" required />
        </FormField>
        <FormField
          id="register-password"
          label="Mot de passe"
          errors={errors.password}
          hint="Au moins 8 caractères, avec une majuscule, une minuscule et un chiffre."
        >
          <input
            id="register-password"
            name="password"
            type="password"
            className="input"
            autoComplete="new-password"
            required
            minLength={8}
          />
        </FormField>

        <FormField
          id="register-phone"
          label={requestAgent ? 'Téléphone' : 'Téléphone (facultatif)'}
          errors={errors.phoneNumber}
          hint="Format international, par exemple +509 3712 3456 ou +1 305 555 1234."
        >
          <input
            id="register-phone"
            name="phoneNumber"
            type="tel"
            className="input"
            autoComplete="tel"
            inputMode="tel"
            required={requestAgent}
          />
        </FormField>

        {requestAgent && (
          <>
            <FormField id="register-message" label="Présentez votre activité (facultatif)" errors={errors.message}>
              <textarea
                id="register-message"
                name="agentRequestMessage"
                className="input"
                rows={3}
                maxLength={1000}
                placeholder="Agence, zone d’activité, type de biens…"
              />
            </FormField>
            <div className={errors.phonePublicConsent ? 'field field--invalid' : 'field'}>
              <label className="checkbox">
                <input type="checkbox" name="phonePublicConsent" required />
                <span>J’accepte que mon numéro de téléphone soit affiché sur mes annonces publiées.</span>
              </label>
              {errors.phonePublicConsent?.map((message) => (
                <p key={message} className="field__error">
                  {message}
                </p>
              ))}
            </div>
          </>
        )}

        <button type="submit" className="button" disabled={pending}>
          {pending ? 'Création…' : requestAgent ? 'Créer mon compte et envoyer ma demande' : 'Créer mon compte'}
        </button>
        <p className="auth__switch">
          Déjà inscrit ? <Link to="/connexion">Se connecter</Link>
        </p>
      </form>
    </div>
  )
}
