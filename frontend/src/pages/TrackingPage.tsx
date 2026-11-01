import { useEffect } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { requestsApi, trackingPath } from '../api/requests'
import { RequestCard } from '../components/RequestList'
import { useApi } from '../useApi'

/** The token in #…, or '' when missing or garbled (the page then says the link is not valid). */
function fragmentToken(hash: string): string {
  try {
    return decodeURIComponent(hash.replace(/^#/, '')).trim()
  } catch {
    return ''
  }
}

/**
 * Follow-up of a request sent without an account, through the private link given after sending it.
 * The token is read from the fragment (/suivi#token), which browsers never send to the server.
 * Older links carried it in the path (/suivi/token): it is moved to the fragment at once, so it does
 * not stay in the address bar or the history; only that first page load reached the server with it.
 */
export function TrackingPage() {
  const { token: pathToken } = useParams()
  const { hash } = useLocation()
  const navigate = useNavigate()
  const token = pathToken ?? fragmentToken(hash)

  useEffect(() => {
    if (pathToken) navigate(trackingPath(pathToken), { replace: true })
  }, [pathToken, navigate])

  return (
    <div className="container page">
      <h1 className="page__title">Suivi de votre demande</h1>
      <p className="page__subtitle">Ce lien est personnel : ne le partagez pas.</p>

      {pathToken ? null /* load only once the token has left the path, so no request sends it as Referer */ : token ? (
        <TrackedRequest token={token} />
      ) : (
        <p className="alert alert--error" role="alert">
          Ce lien de suivi n’est pas valide.
        </p>
      )}
      <p>
        <Link to="/biens">← Voir les biens disponibles</Link>
      </p>
    </div>
  )
}

function TrackedRequest({ token }: { token: string }) {
  const { data, error, loading, reload } = useApi((signal) => requestsApi.track(token, signal), token)

  return (
    <>
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
    </>
  )
}
