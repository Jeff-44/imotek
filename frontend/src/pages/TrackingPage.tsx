import { Link, useParams } from 'react-router'
import { requestsApi } from '../api/requests'
import { RequestCard } from '../components/RequestList'
import { useApi } from '../useApi'

/** Follow-up of a request sent without an account, through the private link given after sending it. */
export function TrackingPage() {
  const { token = '' } = useParams()
  const { data, error, loading, reload } = useApi((signal) => requestsApi.track(token, signal), token)

  return (
    <div className="container page">
      <h1 className="page__title">Suivi de votre demande</h1>
      <p className="page__subtitle">Ce lien est personnel : ne le partagez pas.</p>

      {loading && !data && <p className="page-status" role="status">Chargement…</p>}
      {error && (
        <p className="alert alert--error" role="alert">
          {error.status === 404 ? 'Ce lien de suivi n’est pas valide.' : error.message}
        </p>
      )}
      {data && (
        <ul className="request-list">
          <RequestCard
            request={data}
            viewer="client"
            onChanged={reload}
            perform={(action) => requestsApi.actAsGuest(token, action)}
          />
        </ul>
      )}
      <p>
        <Link to="/biens">← Voir les biens disponibles</Link>
      </p>
    </div>
  )
}
