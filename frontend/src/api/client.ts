import { translateFieldErrors } from './errorMessages'

const TOKEN_KEY = 'imotek.accessToken'

export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function setToken(token: string | null) {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token)
    else localStorage.removeItem(TOKEN_KEY)
  } catch {
    // Storage unavailable (private mode): the session simply won't survive a reload.
  }
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
}

let onUnauthorized: (() => void) | null = null

/** Called when an authenticated request gets a 401 (expired or revoked token). */
export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)

  let response: Response
  try {
    response = await fetch(path, { ...init, headers })
  } catch {
    throw new ApiError(0, 'Impossible de joindre le serveur. Vérifiez votre connexion.')
  }

  if (response.status === 401 && token) onUnauthorized?.()

  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    const message = response.status === 400 || !problem.title
      ? fallbackMessages[response.status] ?? 'Une erreur est survenue.'
      : problem.title
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
