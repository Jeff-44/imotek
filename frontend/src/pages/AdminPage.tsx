import { useState } from 'react'
import { agentRequestsApi } from '../api/auth'
import { ApiError } from '../api/client'
import type { AgentAccount, AgentStatus } from '../api/types'
import { EmptyState } from '../components/EmptyState'
import { useApi } from '../useApi'

type Tab = { status: AgentStatus | 'All'; label: string }

const tabs: Tab[] = [
  { status: 'Pending', label: 'Demandes en attente' },
  { status: 'None', label: 'Agents existants à valider' },
  { status: 'Approved', label: 'Agents validés' },
  { status: 'Rejected', label: 'Demandes refusées' },
]

const dateFormat = new Intl.DateTimeFormat('fr-HT', { dateStyle: 'medium', timeStyle: 'short' })
const formatDate = (value: string | null) => (value ? dateFormat.format(new Date(value)) : '—')

/** Administration — agent requests (step 1). */
export function AdminPage() {
  const [tab, setTab] = useState<Tab>(tabs[0])
  const { data, error, loading, reload } = useApi((signal) => agentRequestsApi.list(tab.status, signal), tab.status)

  return (
    <div className="container page">
      <h1 className="page__title">Administration</h1>
      <p className="page__subtitle">Validation des comptes agents et propriétaires.</p>

      <div className="tabs" role="tablist" aria-label="Demandes agents">
        {tabs.map((t) => (
          <button
            key={t.status}
            type="button"
            role="tab"
            aria-selected={t.status === tab.status}
            className={t.status === tab.status ? 'tabs__tab tabs__tab--active' : 'tabs__tab'}
            onClick={() => setTab(t)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {loading && !data && <p className="page-status" role="status">Chargement…</p>}
      {error && (
        <p className="alert alert--error" role="alert">
          {error.message}
        </p>
      )}
      {data && data.length === 0 && <EmptyState title="Aucun compte dans cette liste" text="Rien à traiter pour le moment." />}
      {data && data.length > 0 && (
        <ul className="manage-list">
          {data.map((account) => (
            <AgentRequestItem key={account.id} account={account} onDecided={reload} />
          ))}
        </ul>
      )}
    </div>
  )
}

function AgentRequestItem({ account, onDecided }: { account: AgentAccount; onDecided: () => void }) {
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const canDecide = account.agentStatus === 'Pending' || (account.isLegacyAgent && account.agentStatus === 'None')

  async function decide(action: () => Promise<unknown>) {
    setBusy(true)
    setError(null)
    try {
      await action()
      onDecided()
    } catch (err) {
      setError(err instanceof ApiError ? Object.values(err.fieldErrors).flat()[0] ?? err.message : 'Action impossible.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <li className="manage-item manage-item--text">
      <div className="manage-item__body">
        {account.isLegacyAgent && account.agentStatus === 'None' && (
          <span className="status status--pending">Legacy Agent – Approval Required</span>
        )}
        <h2 className="manage-item__title">{account.fullName}</h2>
        <p className="manage-item__meta">
          {account.email} · {account.phoneNumber ?? 'Aucun téléphone'}
        </p>
        {account.agentRequestMessage && <p className="manage-item__meta">« {account.agentRequestMessage} »</p>}
        <p className="manage-item__meta">
          Demande : {formatDate(account.agentRequestedAt)}
          {account.agentReviewedAt && ` · Décision : ${formatDate(account.agentReviewedAt)}`}
        </p>
        {account.agentRejectionReason && account.agentStatus === 'Rejected' && (
          <p className="manage-item__meta">Motif du refus : {account.agentRejectionReason}</p>
        )}
        {error && (
          <p className="field__error" role="alert">
            {error}
          </p>
        )}
        {rejecting && (
          <div className="field">
            <label className="field__label" htmlFor={`reason-${account.id}`}>
              Motif du refus
            </label>
            <textarea
              id={`reason-${account.id}`}
              className="input"
              rows={2}
              maxLength={500}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>
        )}
      </div>

      {canDecide && (
        <div className="manage-item__actions">
          {!rejecting ? (
            <>
              <button type="button" className="button" disabled={busy} onClick={() => decide(() => agentRequestsApi.approve(account.id))}>
                Valider
              </button>
              <button type="button" className="button button--danger" disabled={busy} onClick={() => setRejecting(true)}>
                Refuser
              </button>
            </>
          ) : (
            <>
              <button
                type="button"
                className="button button--danger"
                disabled={busy || !reason.trim()}
                onClick={() => decide(() => agentRequestsApi.reject(account.id, reason.trim()))}
              >
                Confirmer le refus
              </button>
              <button type="button" className="button button--ghost" disabled={busy} onClick={() => setRejecting(false)}>
                Annuler
              </button>
            </>
          )}
        </div>
      )}
    </li>
  )
}
