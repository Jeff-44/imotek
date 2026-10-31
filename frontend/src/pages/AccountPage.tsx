import { useState, type FormEvent } from 'react'
import { authApi } from '../api/auth'
import { ApiError } from '../api/client'
import type { User } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { FormField } from '../components/FormField'

/** Profile (name, phone) and the agent request workflow, for every signed-in user. */
export function AccountPage() {
  const { user, updateUser } = useAuth()
  if (!user) return null

  return (
    <div className="container page account">
      <h1 className="page__title">Mon compte</h1>
      <AgentStatusCard user={user} onChange={updateUser} />
      <ProfileForm user={user} onChange={updateUser} />
    </div>
  )
}

function useAction() {
  const [error, setError] = useState<ApiError | null>(null)
  const [pending, setPending] = useState(false)
  const [done, setDone] = useState<string | null>(null)

  async function run(action: () => Promise<void>, success: string) {
    setPending(true)
    setError(null)
    setDone(null)
    try {
      await action()
      setDone(success)
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, 'Une erreur est survenue.'))
    } finally {
      setPending(false)
    }
  }

  return { error, pending, done, run }
}

const field = (data: FormData, name: string) => String(data.get(name) ?? '').trim()

function ProfileForm({ user, onChange }: { user: User; onChange: (user: User) => void }) {
  const { error, pending, done, run } = useAction()
  const isAgentAccount = user.roles.includes('Agent') || user.agentStatus === 'Pending' || user.agentStatus === 'Approved'
  const errors = error?.fieldErrors ?? {}

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    void run(async () => {
      onChange(
        await authApi.updateMe({
          fullName: field(data, 'fullName'),
          phoneNumber: field(data, 'phoneNumber'),
          phonePublicConsent: data.get('phonePublicConsent') === 'on',
        }),
      )
    }, 'Profil enregistré.')
  }

  return (
    <form className="card account__card" onSubmit={handleSubmit} noValidate aria-labelledby="profile-title">
      <h2 id="profile-title" className="account__title">
        Profil
      </h2>
      {error && !Object.keys(errors).length && (
        <p className="alert alert--error" role="alert">
          {error.message}
        </p>
      )}
      {done && (
        <p className="alert alert--success" role="status">
          {done}
        </p>
      )}
      <p className="field__hint">Adresse e-mail : {user.email}</p>
      <FormField id="profile-name" label="Nom complet" errors={errors.fullName}>
        <input id="profile-name" name="fullName" className="input" defaultValue={user.fullName} required maxLength={150} />
      </FormField>
      <FormField
        id="profile-phone"
        label={isAgentAccount ? 'Téléphone' : 'Téléphone (facultatif)'}
        errors={errors.phoneNumber}
        hint="Format international, par exemple +509 3712 3456 ou +1 305 555 1234."
      >
        <input
          id="profile-phone"
          name="phoneNumber"
          type="tel"
          className="input"
          autoComplete="tel"
          defaultValue={user.phoneNumber ?? ''}
          required={isAgentAccount}
        />
      </FormField>
      <div className={errors.phonePublicConsent ? 'field field--invalid' : 'field'}>
        <label className="checkbox">
          <input type="checkbox" name="phonePublicConsent" defaultChecked={user.phonePublicConsent} />
          <span>J’accepte que mon numéro de téléphone soit affiché sur mes annonces publiées.</span>
        </label>
        {errors.phonePublicConsent?.map((message) => (
          <p key={message} className="field__error">
            {message}
          </p>
        ))}
      </div>
      <button type="submit" className="button" disabled={pending}>
        {pending ? 'Enregistrement…' : 'Enregistrer'}
      </button>
    </form>
  )
}

const statusText: Record<User['agentStatus'], string> = {
  None: 'Vous n’avez pas encore demandé à publier des annonces.',
  Pending: 'Votre demande pour publier des annonces est en cours d’examen par un administrateur.',
  Approved: 'Votre compte agent est validé : vous pouvez publier des annonces.',
  Rejected: 'Votre demande a été refusée.',
}

function AgentStatusCard({ user, onChange }: { user: User; onChange: (user: User) => void }) {
  const { error, pending, done, run } = useAction()
  const canRequest = !user.roles.includes('Admin') && (user.agentStatus === 'None' || user.agentStatus === 'Rejected')
  const errors = error?.fieldErrors ?? {}

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    void run(async () => {
      onChange(
        await authApi.requestAgent({
          phoneNumber: field(data, 'phoneNumber'),
          phonePublicConsent: data.get('phonePublicConsent') === 'on',
          message: field(data, 'message'),
        }),
      )
    }, 'Demande envoyée. Un administrateur va l’examiner.')
  }

  if (user.roles.includes('Admin')) return null

  return (
    <section className="card account__card" aria-labelledby="agent-title">
      <h2 id="agent-title" className="account__title">
        Publier des annonces
      </h2>

      {user.isLegacyAgent && (
        <p className="alert alert--warning" role="status">
          Compte agent existant en attente de validation par un administrateur. Vous pouvez consulter et retirer vos
          annonces, mais pas en publier ni en modifier.
        </p>
      )}

      <p data-testid="agent-status">{statusText[user.agentStatus]}</p>
      {user.agentStatus === 'Rejected' && user.agentRejectionReason && (
        <p className="field__hint">Motif : {user.agentRejectionReason}</p>
      )}
      {user.agentStatus === 'Approved' && !user.roles.includes('Agent') && (
        <p className="alert alert--info">Reconnectez-vous pour activer votre espace agent.</p>
      )}

      {done && (
        <p className="alert alert--success" role="status">
          {done}
        </p>
      )}
      {error && !Object.keys(errors).length && (
        <p className="alert alert--error" role="alert">
          {error.message}
        </p>
      )}

      {canRequest && (
        <form className="account__form" onSubmit={handleSubmit} noValidate>
          <FormField id="request-phone" label="Téléphone" errors={errors.phoneNumber}>
            <input
              id="request-phone"
              name="phoneNumber"
              type="tel"
              className="input"
              defaultValue={user.phoneNumber ?? ''}
              required
            />
          </FormField>
          <FormField id="request-message" label="Présentez votre activité (facultatif)" errors={errors.message}>
            <textarea id="request-message" name="message" className="input" rows={3} maxLength={1000} />
          </FormField>
          <div className={errors.phonePublicConsent ? 'field field--invalid' : 'field'}>
            <label className="checkbox">
              <input type="checkbox" name="phonePublicConsent" />
              <span>J’accepte que mon numéro de téléphone soit affiché sur mes annonces publiées.</span>
            </label>
            {errors.phonePublicConsent?.map((message) => (
              <p key={message} className="field__error">
                {message}
              </p>
            ))}
          </div>
          <button type="submit" className="button" disabled={pending}>
            {pending ? 'Envoi…' : user.agentStatus === 'Rejected' ? 'Envoyer une nouvelle demande' : 'Demander à devenir agent'}
          </button>
        </form>
      )}
    </section>
  )
}
