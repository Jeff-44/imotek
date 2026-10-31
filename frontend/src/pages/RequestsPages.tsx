import { Link } from 'react-router'
import { requestsApi } from '../api/requests'
import { RequestList } from '../components/RequestList'
import { useApi } from '../useApi'

/** "Mes demandes": the client's rental, purchase and visit requests. */
export function MyRequestsPage() {
  const { data, error, loading, reload } = useApi((signal) => requestsApi.mine(signal), 'mine')

  return (
    <div className="container page">
      <div className="page__header">
        <div>
          <h1 className="page__title">Mes demandes</h1>
          <p className="page__subtitle">Suivez vos demandes de location, d’achat et de visite.</p>
        </div>
        <Link to="/biens" className="button button--ghost">
          Chercher un bien
        </Link>
      </div>
      {loading && !data && <p className="page-status" role="status">Chargement…</p>}
      {error && (
        <p className="alert alert--error" role="alert">
          {error.message}
        </p>
      )}
      {data && <RequestList requests={data} viewer="client" onChanged={reload} />}
    </div>
  )
}

/** "Demandes reçues": requests about the agent's properties. */
export function ReceivedRequestsPage() {
  const { data, error, loading, reload } = useApi((signal) => requestsApi.received(signal), 'received')
  const open = data?.filter((r) => r.allowedActions.length > 0).length ?? 0

  return (
    <div className="container page">
      <div className="page__header">
        <div>
          <h1 className="page__title">Demandes reçues</h1>
          <p className="page__subtitle">
            {data ? `${open} demande${open > 1 ? 's' : ''} à traiter.` : 'Demandes des clients sur vos biens.'}
          </p>
        </div>
        <Link to="/espace-agent" className="button button--ghost">
          Mes biens
        </Link>
      </div>
      {loading && !data && <p className="page-status" role="status">Chargement…</p>}
      {error && (
        <p className="alert alert--error" role="alert">
          {error.message}
        </p>
      )}
      {data && <RequestList requests={data} viewer="agent" onChanged={reload} />}
    </div>
  )
}
