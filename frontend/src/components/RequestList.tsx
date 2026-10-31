import { useState } from 'react'
import { Link } from 'react-router'
import { ApiError } from '../api/client'
import {
  localInputToIso,
  requestsApi,
  requestStatusLabels,
  requestTypeLabels,
  type ClientRequest,
  type RequestAction,
  type RequestStatus,
  type RequestType,
} from '../api/requests'
import { formatDateTime } from '../format'
import { ContactLinks } from './PropertyActions'
import { EmptyState } from './EmptyState'

const statusClass: Record<RequestStatus, string> = {
  Pending: 'pending',
  Responded: 'pending',
  Rescheduled: 'pending',
  Accepted: 'available',
  Declined: 'declined',
  Cancelled: 'draft',
  Closed: 'draft',
}

export function RequestStatusBadge({ status }: { status: RequestStatus }) {
  return <span className={`status status--${statusClass[status]}`}>{requestStatusLabels[status]}</span>
}

const actionLabels: Record<RequestAction, string> = {
  Respond: 'Répondre',
  Accept: 'Accepter',
  Decline: 'Refuser',
  ProposeDate: 'Proposer cette date',
  Close: 'Clôturer',
  Cancel: 'Annuler ma demande',
  AcceptProposal: 'Accepter la nouvelle date',
}

const tabs: { type: RequestType | 'All'; label: string }[] = [
  { type: 'All', label: 'Toutes' },
  { type: 'Rental', label: 'Location' },
  { type: 'Purchase', label: 'Achat' },
  { type: 'Visit', label: 'Visite' },
]

type Viewer = 'client' | 'agent'

/** Requests of the client ("Mes demandes") or received by the agent, filtered by kind. */
export function RequestList({ requests, viewer, onChanged }: { requests: ClientRequest[]; viewer: Viewer; onChanged: () => void }) {
  const [tab, setTab] = useState<RequestType | 'All'>('All')
  const shown = tab === 'All' ? requests : requests.filter((r) => r.type === tab)

  return (
    <>
      <div className="tabs" role="tablist" aria-label="Type de demande">
        {tabs.map((t) => {
          const count = t.type === 'All' ? requests.length : requests.filter((r) => r.type === t.type).length
          return (
            <button
              key={t.type}
              type="button"
              role="tab"
              aria-selected={t.type === tab}
              className={t.type === tab ? 'tabs__tab tabs__tab--active' : 'tabs__tab'}
              onClick={() => setTab(t.type)}
            >
              {t.label} ({count})
            </button>
          )
        })}
      </div>

      {shown.length === 0 ? (
        <EmptyState
          title="Aucune demande"
          text={viewer === 'client' ? 'Vos demandes de location, d’achat et de visite apparaîtront ici.' : 'Les demandes des clients sur vos biens apparaîtront ici.'}
        />
      ) : (
        <ul className="request-list">
          {shown.map((request) => (
            <RequestItem key={request.id} request={request} viewer={viewer} onChanged={onChanged} />
          ))}
        </ul>
      )}
    </>
  )
}

function RequestItem({ request, viewer, onChanged }: { request: ClientRequest; viewer: Viewer; onChanged: () => void }) {
  const [message, setMessage] = useState('')
  const [proposedAt, setProposedAt] = useState('')
  const [busy, setBusy] = useState<RequestAction | null>(null)
  const [error, setError] = useState<string | null>(null)
  const actions = request.allowedActions
  const agentActions = actions.filter((a) => a !== 'ProposeDate')

  async function act(action: RequestAction) {
    if (action === 'Cancel' && !window.confirm('Annuler cette demande ?')) return
    if (action === 'ProposeDate' && !proposedAt) {
      setError('Choisissez la date à proposer.')
      return
    }
    setBusy(action)
    setError(null)
    try {
      await requestsApi.act(request.id, action, {
        message: viewer === 'agent' ? message.trim() || undefined : undefined,
        proposedVisitAt: action === 'ProposeDate' ? localInputToIso(proposedAt) : undefined,
      })
      setMessage('')
      onChanged()
    } catch (err) {
      setError(err instanceof ApiError ? (Object.values(err.fieldErrors).flat()[0] ?? err.message) : 'Action impossible.')
    } finally {
      setBusy(null)
    }
  }

  const party = viewer === 'agent' ? request.client : request.agent

  return (
    <li className="request-item" data-testid="request-item">
      <div className="request-item__header">
        <span className="tag tag--accent">{requestTypeLabels[request.type]}</span>
        <RequestStatusBadge status={request.status} />
        <span className="request-item__date">Envoyée le {formatDateTime(request.createdAt)}</span>
      </div>

      <h2 className="manage-item__title">
        <Link to={`/biens/${request.propertyId}`}>{request.propertyTitle || 'Bien'}</Link>
        {request.propertyCity && <span className="request-item__city"> · {request.propertyCity}</span>}
      </h2>

      {request.visitAt && (
        <p className="request-item__meta">
          Visite : <strong>{formatDateTime(request.visitAt)}</strong>
          {request.proposedVisitAt && (
            <>
              {' '}
              · Nouvelle date proposée : <strong>{formatDateTime(request.proposedVisitAt)}</strong>
            </>
          )}
        </p>
      )}

      {party && viewer === 'agent' && (
        <div className="request-item__party">
          <p>
            Client : <strong>{party.fullName}</strong>
            {party.email && (
              <>
                {' '}
                · <a href={`mailto:${party.email}`}>{party.email}</a>
              </>
            )}
          </p>
          {party.phoneNumber ? (
            <ContactLinks phone={party.phoneNumber} />
          ) : (
            <p className="field__hint">Le client n’a pas partagé son numéro pour cette demande.</p>
          )}
        </div>
      )}
      {party && viewer === 'client' && <p className="request-item__meta">Agent : {party.fullName}</p>}

      {request.message && (
        <blockquote className="request-item__quote">
          <span className="request-item__quote-label">{viewer === 'agent' ? 'Message du client' : 'Votre message'}</span>
          {request.message}
        </blockquote>
      )}
      {request.agentResponse && (
        <blockquote className="request-item__quote request-item__quote--agent">
          <span className="request-item__quote-label">{viewer === 'agent' ? 'Votre réponse' : 'Réponse de l’agent'}</span>
          {request.agentResponse}
        </blockquote>
      )}

      {error && (
        <p className="alert alert--error" role="alert">
          {error}
        </p>
      )}

      {viewer === 'agent' && actions.length > 0 && (
        <div className="request-item__answer">
          <label className="field__label" htmlFor={`answer-${request.id}`}>
            Message au client (facultatif)
          </label>
          <textarea
            id={`answer-${request.id}`}
            className="input"
            rows={2}
            maxLength={1000}
            value={message}
            onChange={(e) => setMessage(e.target.value)}
          />
          {actions.includes('ProposeDate') && (
            <div className="request-item__propose">
              <label className="field__label" htmlFor={`propose-${request.id}`}>
                Autre date de visite
              </label>
              <input
                id={`propose-${request.id}`}
                type="datetime-local"
                className="input"
                value={proposedAt}
                onChange={(e) => setProposedAt(e.target.value)}
              />
              <button type="button" className="button button--ghost" disabled={busy !== null} onClick={() => act('ProposeDate')}>
                {busy === 'ProposeDate' ? '…' : actionLabels.ProposeDate}
              </button>
            </div>
          )}
        </div>
      )}

      {(viewer === 'agent' ? agentActions : actions).length > 0 && (
        <div className="request-item__actions">
          {(viewer === 'agent' ? agentActions : actions).map((action) => (
            <button
              key={action}
              type="button"
              className={action === 'Decline' || action === 'Cancel' ? 'button button--danger' : action === 'Accept' || action === 'AcceptProposal' ? 'button' : 'button button--ghost'}
              disabled={busy !== null}
              onClick={() => act(action)}
            >
              {busy === action ? '…' : actionLabels[action]}
            </button>
          ))}
        </div>
      )}
    </li>
  )
}
