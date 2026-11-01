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
  /** Sent by a visitor without an account: `client` holds the details they typed. */
  isGuest: boolean
  // Rental requests only.
  currentAddress: string | null
  durationMonths: number | null
  /** yyyy-mm-dd */
  moveInDate: string | null
  occupants: number | null
  /** Only in the answer to a visitor's new request: the secret of their follow-up link. */
  trackingToken?: string | null
}

export type NewRequest = {
  type: RequestType
  message?: string
  shareClientPhone: boolean
  /** ISO date with its offset, visits only. */
  visitAt?: string
  // Visitors without an account.
  fullName?: string
  email?: string
  phoneNumber?: string
  // Rentals.
  currentAddress?: string
  durationMonths?: number
  /** yyyy-mm-dd */
  moveInDate?: string
  occupants?: number
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

const TRACKING_TOKEN_HEADER = 'X-Tracking-Token'

/**
 * A visitor's follow-up link. The token is in the fragment (after #): browsers never send it to the
 * server, so it stays out of every access log; the page then passes it to the API in a header.
 */
export const trackingPath = (token: string) => `/suivi#${token}`

export const requestsApi = {
  contact: (propertyId: string, signal?: AbortSignal) =>
    apiFetch<AgentContact>(`/api/properties/${id(propertyId)}/contact`, { signal }),

  send: (propertyId: string, input: NewRequest) =>
    apiFetch<ClientRequest>(`/api/properties/${id(propertyId)}/requests`, { method: 'POST', body: JSON.stringify(input) }),

  mine: (signal?: AbortSignal) => apiFetch<ClientRequest[]>('/api/requests/mine', { signal }),

  received: (signal?: AbortSignal) => apiFetch<ClientRequest[]>('/api/requests/received', { signal }),

  /** A visitor's request, through their private follow-up link. The token goes in a header, never in a URL. */
  track: (token: string, signal?: AbortSignal) =>
    apiFetch<ClientRequest>('/api/requests/track', { signal, headers: { [TRACKING_TOKEN_HEADER]: token } }),

  actAsGuest: (token: string, action: RequestAction) =>
    apiFetch<ClientRequest>(`/api/requests/track/${actionPaths[action]}`, {
      method: 'POST',
      headers: { [TRACKING_TOKEN_HEADER]: token },
    }),

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

/** A date (yyyy-mm-dd) and a time (hh:mm) typed in local time → ISO string. */
export function localDateTimeToIso(date: string, time: string): string {
  return new Date(`${date}T${time}`).toISOString()
}
