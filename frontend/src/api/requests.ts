import { apiFetch } from './client'

export type RequestType = 'Rental' | 'Purchase' | 'Visit'
export type RequestStatus = 'Pending' | 'Responded' | 'Accepted' | 'Rescheduled' | 'Declined' | 'Cancelled' | 'Closed'
export type RequestAction = 'Respond' | 'Accept' | 'Decline' | 'ProposeDate' | 'Close' | 'Cancel' | 'AcceptProposal'

export type RequestParty = { fullName: string; email: string | null; phoneNumber: string | null }

export type ClientRequest = {
  id: string
  type: RequestType
  status: RequestStatus
  propertyId: string
  propertyTitle: string
  propertyCity: string
  message: string | null
  shareClientPhone: boolean
  agentResponse: string | null
  visitAt: string | null
  proposedVisitAt: string | null
  createdAt: string
  updatedAt: string
  /** Seen by the agent: the client, with the phone only when shared for this request. */
  client: RequestParty | null
  /** Seen by the client: the agent's name. */
  agent: RequestParty | null
  /** What the current user may do now. */
  allowedActions: RequestAction[]
}

export type NewRequest = {
  type: RequestType
  message?: string
  shareClientPhone: boolean
  /** ISO date with its offset, visits only. */
  visitAt?: string
}

export type ActionInput = { message?: string; proposedVisitAt?: string }

export type NotificationKind =
  | 'RequestReceived'
  | 'RequestCancelled'
  | 'ProposalAccepted'
  | 'RequestResponded'
  | 'RequestAccepted'
  | 'RequestDeclined'
  | 'RequestClosed'
  | 'VisitRescheduled'

export type AppNotification = {
  id: string
  kind: NotificationKind
  requestId: string
  requestType: RequestType
  propertyId: string
  propertyTitle: string
  createdAt: string
  isRead: boolean
}

export type NotificationList = { items: AppNotification[]; unreadCount: number }

export type AgentContact = { agentName: string; phoneNumber: string | null }

export const requestTypeLabels: Record<RequestType, string> = {
  Rental: 'Location',
  Purchase: 'Achat',
  Visit: 'Visite',
}

export const requestStatusLabels: Record<RequestStatus, string> = {
  Pending: 'En attente',
  Responded: 'Réponse reçue',
  Accepted: 'Acceptée',
  Rescheduled: 'Nouvelle date proposée',
  Declined: 'Refusée',
  Cancelled: 'Annulée',
  Closed: 'Clôturée',
}

const actionPaths: Record<RequestAction, string> = {
  Respond: 'respond',
  Accept: 'accept',
  Decline: 'decline',
  ProposeDate: 'propose-date',
  Close: 'close',
  Cancel: 'cancel',
  AcceptProposal: 'accept-proposal',
}

const id = (value: string) => encodeURIComponent(value)

export const requestsApi = {
  contact: (propertyId: string, signal?: AbortSignal) =>
    apiFetch<AgentContact>(`/api/properties/${id(propertyId)}/contact`, { signal }),

  send: (propertyId: string, input: NewRequest) =>
    apiFetch<ClientRequest>(`/api/properties/${id(propertyId)}/requests`, { method: 'POST', body: JSON.stringify(input) }),

  mine: (signal?: AbortSignal) => apiFetch<ClientRequest[]>('/api/requests/mine', { signal }),

  received: (signal?: AbortSignal) => apiFetch<ClientRequest[]>('/api/requests/received', { signal }),

  act: (requestId: string, action: RequestAction, input: ActionInput = {}) =>
    apiFetch<ClientRequest>(`/api/requests/${id(requestId)}/${actionPaths[action]}`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
}

export const notificationsApi = {
  list: (signal?: AbortSignal) => apiFetch<NotificationList>('/api/notifications', { signal }),
  markRead: (notificationId: string) => apiFetch<void>(`/api/notifications/${id(notificationId)}/read`, { method: 'POST' }),
  markAllRead: () => apiFetch<void>('/api/notifications/read-all', { method: 'POST' }),
}

/** Digits only, as wa.me expects them. */
export function contactLinks(phoneE164: string) {
  const digits = phoneE164.replace(/\D/g, '')
  return {
    call: `tel:+${digits}`,
    whatsApp: `https://wa.me/${digits}`,
    sms: `sms:+${digits}`,
  }
}

/** Value for <input type="datetime-local"> → ISO string with the browser's offset. */
export function localInputToIso(value: string): string {
  return new Date(value).toISOString()
}
