import { useState } from 'react'
import { useNavigate } from 'react-router'
import { notificationsApi, type AppNotification, type NotificationKind, type RequestType } from '../api/requests'
import { EmptyState } from '../components/EmptyState'
import { formatDateTime } from '../format'
import { useApi } from '../useApi'

const typeNames: Record<RequestType, string> = { Rental: 'location', Purchase: 'achat', Visit: 'visite' }

const texts: Record<NotificationKind, (type: string) => string> = {
  RequestReceived: (type) => `Nouvelle demande de ${type}`,
  RequestCancelled: (type) => `Demande de ${type} annulée par le client`,
  ProposalAccepted: () => 'Le client a accepté la nouvelle date de visite',
  RequestResponded: (type) => `L’agent a répondu à votre demande de ${type}`,
  RequestAccepted: (type) => `Votre demande de ${type} a été acceptée`,
  RequestDeclined: (type) => `Votre demande de ${type} a été refusée`,
  RequestClosed: (type) => `Votre demande de ${type} a été clôturée`,
  VisitRescheduled: () => 'L’agent propose une nouvelle date de visite',
}

/** Notices for the agent lead to the received requests; the others to the client's requests. */
const forAgent: NotificationKind[] = ['RequestReceived', 'RequestCancelled', 'ProposalAccepted']

function notificationText(notification: AppNotification): string {
  return texts[notification.kind](typeNames[notification.requestType])
}

export function NotificationsPage() {
  const navigate = useNavigate()
  const { data, error, loading, reload } = useApi((signal) => notificationsApi.list(signal), 'notifications')
  const [busy, setBusy] = useState(false)

  async function open(notification: AppNotification) {
    if (!notification.isRead) await notificationsApi.markRead(notification.id).catch(() => undefined)
    navigate(forAgent.includes(notification.kind) ? '/espace-agent/demandes' : '/mes-demandes')
  }

  async function markAll() {
    setBusy(true)
    try {
      await notificationsApi.markAllRead()
      reload()
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="container page">
      <div className="page__header">
        <div>
          <h1 className="page__title">Notifications</h1>
          <p className="page__subtitle">
            {data ? `${data.unreadCount} non lue${data.unreadCount > 1 ? 's' : ''}` : 'Vos dernières notifications.'}
          </p>
        </div>
        {data && data.unreadCount > 0 && (
          <button type="button" className="button button--ghost" onClick={markAll} disabled={busy}>
            Tout marquer comme lu
          </button>
        )}
      </div>
      {loading && !data && <p className="page-status" role="status">Chargement…</p>}
      {error && (
        <p className="alert alert--error" role="alert">
          {error.message}
        </p>
      )}
      {data && data.items.length === 0 && (
        <EmptyState title="Aucune notification" text="Vous serez prévenu ici des nouvelles demandes et des réponses." />
      )}
      {data && data.items.length > 0 && (
        <ul className="notification-list">
          {data.items.map((n) => (
            <li key={n.id}>
              <button
                type="button"
                className={n.isRead ? 'notification' : 'notification notification--unread'}
                onClick={() => open(n)}
              >
                <span className="notification__text">
                  {notificationText(n)}
                  {n.propertyTitle && <> — « {n.propertyTitle} »</>}
                </span>
                <span className="notification__date">{formatDateTime(n.createdAt)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
