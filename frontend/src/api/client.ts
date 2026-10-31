import { translateError, translateFieldErrors } from './errorMessages'

const ACCESS_KEY = 'imotek.accessToken'
const REFRESH_KEY = 'imotek.refreshToken'

export type Tokens = { accessToken: string; refreshToken: string }

function read(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function write(key: string, value: string | null) {
  try {
    if (value) localStorage.setItem(key, value)
    else localStorage.removeItem(key)
  } catch {
    // Storage unavailable (private mode): the session simply won't survive a reload.
  }
}

export function getToken(): string | null {
  return read(ACCESS_KEY)
}

export function setTokens(tokens: Tokens | null) {
  write(ACCESS_KEY, tokens?.accessToken ?? null)
  write(REFRESH_KEY, tokens?.refreshToken ?? null)
}

/** An HTTP error with the ProblemDetails returned by the API. */
export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>
  /** Seconds to wait before retrying, from the Retry-After header (429, 503). */
  readonly retryAfter: number | null

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}, retryAfter: number | null = null) {
    super(message)
    this.status = status
    this.fieldErrors = fieldErrors
    this.retryAfter = retryAfter
  }

  /** Rate limiting, server errors and network failures: worth retrying, and no reason to sign out. */
  get isTemporary(): boolean {
    return this.status === 0 || this.status === 429 || this.status >= 500
  }
}

/** Retry-After in seconds (delta or HTTP date), or null. */
function retryAfterOf(response: Response): number | null {
  const value = response.headers.get('Retry-After')
  if (!value) return null
  const seconds = /^\d+$/.test(value) ? Number(value) : Math.ceil((Date.parse(value) - Date.now()) / 1000)
  return Number.isFinite(seconds) ? Math.max(0, seconds) : null
}

const networkError = () => new ApiError(0, 'Impossible de joindre le serveur. Vérifiez votre connexion.')

function temporaryError(response: Response): ApiError {
  const retryAfter = retryAfterOf(response)
  const message =
    response.status === 429
      ? retryAfter
        ? `Trop de requêtes. Réessayez dans ${retryAfter} s.`
        : fallbackMessages[429]
      : 'Le serveur est momentanément indisponible. Réessayez dans un instant.'
  return new ApiError(response.status, message, {}, retryAfter)
}

type ProblemDetails = { title?: string; errors?: Record<string, string[]> }

const fallbackMessages: Record<number, string> = {
  400: 'Certaines informations sont invalides.',
  401: 'Veuillez vous connecter.',
  403: 'Vous n’avez pas l’autorisation d’effectuer cette action.',
  404: 'Ressource introuvable.',
  409: 'Cette ressource existe déjà.',
  413: 'Le fichier est trop volumineux.',
  429: 'Trop de tentatives. Patientez une minute puis réessayez.',
}

let onSessionExpired: (() => void) | null = null

/** Called when the session can no longer be renewed (refresh token expired or revoked). */
export function setSessionExpiredHandler(handler: (() => void) | null) {
  onSessionExpired = handler
}

/** Renewed; rejected (expired or revoked: the session is over); or a temporary error (the session is kept). */
type RenewOutcome = 'renewed' | 'rejected' | ApiError

let refreshing: Promise<RenewOutcome> | null = null

/**
 * Exchanges the refresh token for new tokens. Concurrent callers share one request,
 * because a refresh token is only meant to be used once.
 */
function renew(): Promise<RenewOutcome> {
  const refreshToken = read(REFRESH_KEY)
  if (!refreshToken) return Promise.resolve('rejected')

  refreshing ??= (async (): Promise<RenewOutcome> => {
    try {
      const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (response.ok) {
        setTokens((await response.json()) as Tokens)
        return 'renewed'
      }
      return response.status === 429 || response.status >= 500 ? temporaryError(response) : 'rejected'
    } catch {
      return networkError()
    } finally {
      refreshing = null
    }
  })()
  return refreshing
}

/** True when new tokens were obtained. */
export async function refreshSession(): Promise<boolean> {
  return (await renew()) === 'renewed'
}

async function send(path: string, init: RequestInit): Promise<Response> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  // FormData sets its own multipart Content-Type with the boundary.
  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)

  try {
    return await fetch(path, { ...init, headers })
  } catch {
    throw networkError()
  }
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const hadToken = getToken() !== null
  let response = await send(path, init)

  // Expired access token: renew it once and replay the request. Only a refused refresh token
  // ends the session; a rate limit, a server error or a network failure keeps it for a retry.
  if (response.status === 401 && hadToken) {
    const outcome = await renew()
    if (outcome === 'renewed') {
      response = await send(path, init)
    } else if (outcome === 'rejected') {
      setTokens(null)
      onSessionExpired?.()
    } else {
      throw outcome
    }
  }

  if (response.status === 429 || response.status >= 500) throw temporaryError(response)

  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    const message =
      response.status === 400 || !problem.title
        ? fallbackMessages[response.status] ?? 'Une erreur est survenue.'
        : translateError(problem.title)
    throw new ApiError(response.status, message, translateFieldErrors(problem.errors ?? {}))
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export function toQueryString(params: Record<string, string | number | undefined | null>): string {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') query.set(key, String(value))
  }
  const text = query.toString()
  return text ? `?${text}` : ''
}
