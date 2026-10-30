import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiFetch, getToken, refreshSession, setSessionExpiredHandler, setTokens } from './client'

function memoryStorage(): Storage {
  const data = new Map<string, string>()
  return {
    get length() {
      return data.size
    },
    clear: () => data.clear(),
    getItem: (key) => data.get(key) ?? null,
    key: (index) => [...data.keys()][index] ?? null,
    removeItem: (key) => void data.delete(key),
    setItem: (key, value) => void data.set(key, value),
  }
}

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

type FetchCall = { url: string; init: RequestInit }

describe('apiFetch token handling', () => {
  let calls: FetchCall[]
  let responses: Response[]

  beforeEach(() => {
    vi.stubGlobal('localStorage', memoryStorage())
    calls = []
    responses = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init: RequestInit) => {
        calls.push({ url, init })
        const next = responses.shift()
        if (!next) throw new Error(`Unexpected request to ${url}`)
        return next
      }),
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    setSessionExpiredHandler(null)
  })

  const authHeader = (call: FetchCall) => new Headers(call.init.headers).get('Authorization')

  it('sends the access token', async () => {
    setTokens({ accessToken: 'a1', refreshToken: 'r1' })
    responses.push(json(200, { ok: true }))

    await apiFetch('/api/auth/me')

    expect(authHeader(calls[0])).toBe('Bearer a1')
  })

  it('renews an expired access token once and replays the request', async () => {
    setTokens({ accessToken: 'expired', refreshToken: 'r1' })
    responses.push(json(401), json(200, { accessToken: 'a2', refreshToken: 'r2' }), json(200, { id: 1 }))

    const result = await apiFetch<{ id: number }>('/api/properties/my-properties')

    expect(result).toEqual({ id: 1 })
    expect(calls.map((c) => c.url)).toEqual(['/api/properties/my-properties', '/api/auth/refresh', '/api/properties/my-properties'])
    expect(JSON.parse(String(calls[1].init.body))).toEqual({ refreshToken: 'r1' })
    expect(authHeader(calls[2])).toBe('Bearer a2')
    expect(getToken()).toBe('a2')
  })

  it('clears the session when the refresh token is rejected', async () => {
    setTokens({ accessToken: 'expired', refreshToken: 'revoked' })
    const expired = vi.fn()
    setSessionExpiredHandler(expired)
    responses.push(json(401), json(401, { title: 'The session has expired. Please sign in again.' }), json(401))

    await expect(apiFetch('/api/properties/my-properties')).rejects.toBeInstanceOf(ApiError)

    expect(expired).toHaveBeenCalledOnce()
    expect(getToken()).toBeNull()
  })

  it('does not try to refresh when the user was not signed in', async () => {
    responses.push(json(401, { title: 'Invalid email or password.' }))

    const error = await apiFetch('/api/auth/login', { method: 'POST', body: '{}' }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).message).toBe('Adresse e-mail ou mot de passe incorrect.')
    expect(calls).toHaveLength(1)
  })

  it('shares one refresh request between concurrent callers', async () => {
    setTokens({ accessToken: 'a1', refreshToken: 'r1' })
    responses.push(json(200, { accessToken: 'a2', refreshToken: 'r2' }))

    const [first, second] = await Promise.all([refreshSession(), refreshSession()])

    expect(first && second).toBe(true)
    expect(calls.filter((c) => c.url === '/api/auth/refresh')).toHaveLength(1)
  })

  it('lets the browser set the multipart Content-Type for uploads', async () => {
    responses.push(json(200, {}))

    await apiFetch('/api/properties/1/images', { method: 'POST', body: new FormData() })

    expect(new Headers(calls[0].init.headers).has('Content-Type')).toBe(false)
  })

  it('translates validation errors per field', async () => {
    responses.push(json(400, { title: 'One or more validation errors occurred.', errors: { currency: ['Currency is required when filtering by price.'] } }))

    const error = (await apiFetch('/api/properties?minPrice=1').catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(400)
    expect(error.fieldErrors).toEqual({ currency: ['Choisissez une devise pour filtrer par prix.'] })
  })
})
