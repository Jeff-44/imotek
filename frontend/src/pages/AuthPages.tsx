import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation } from 'react-router'
import { ApiError } from '../api/client'
import type { User } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { FormField } from '../components/FormField'

/** Once signed in, go back where the user came from, or to the page that fits their role. */
function SignedInRedirect({ user }: { user: User }) {
  const from = (useLocation().state as { from?: string } | null)?.from
  return <Navigate to={from ?? (user.roles.includes('Agent') ? '/espace-agent' : '/biens')} replace />
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
  const { error, pending, onSubmit } = useSubmit(async (data) => {
    await register({
      email: field(data, 'email'),
      password: field(data, 'password'),
      fullName: field(data, 'fullName'),
      role: field(data, 'role') === 'Agent' ? 'Agent' : 'Client',
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
          <legend className="field__label">Je suis</legend>
          <div className="segmented">
            <label className="segmented__option">
              <input type="radio" name="role" value="Client" defaultChecked />
              <span>Je cherche un bien</span>
            </label>
            <label className="segmented__option">
              <input type="radio" name="role" value="Agent" />
              <span>Agent / propriétaire</span>
            </label>
          </div>
        </fieldset>

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

        <button type="submit" className="button" disabled={pending}>
          {pending ? 'Création…' : 'Créer mon compte'}
        </button>
        <p className="auth__switch">
          Déjà inscrit ? <Link to="/connexion">Se connecter</Link>
        </p>
      </form>
    </div>
  )
}
