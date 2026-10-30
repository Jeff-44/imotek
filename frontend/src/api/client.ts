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

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.status = status
    this.fieldErrors = fieldErrors
  }
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

let refreshing: Promise<boolean> | null = null

/**
 * Exchanges the refresh token for new tokens. Concurrent callers share one request,
 * because a refresh token is only meant to be used once.
 */
export function refreshSession(): Promise<boolean> {
  const refreshToken = read(REFRESH_KEY)
  if (!refreshToken) return Promise.resolve(false)

  refreshing ??= (async () => {
    try {
      const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({ refreshToken }),
      })
      if (!response.ok) return false
      setTokens((await response.json()) as Tokens)
      return true
    } catch {
      return false
    } finally {
      refreshing = null
    }
  })()
  return refreshing
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
    throw new ApiError(0, 'Impossible de joindre le serveur. Vérifiez votre connexion.')
  }
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const hadToken = getToken() !== null
  let response = await send(path, init)

  // Expired access token: renew it once and replay the request.
  if (response.status === 401 && hadToken) {
    if (await refreshSession()) {
      response = await send(path, init)
    } else {
      setTokens(null)
      onSessionExpired?.()
    }
  }

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
