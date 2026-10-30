import { useCallback, useEffect, useEffectEvent, useState } from 'react'
import { ApiError } from './api/client'

type ApiState<T> = {
  data: T | null
  error: ApiError | null
  loading: boolean
  reload: () => void
}

type Loaded<T> = { requestKey: string | null; data: T | null; error: ApiError | null }

/** Runs `load` whenever `key` changes, cancelling the previous request. */
export function useApi<T>(load: (signal: AbortSignal) => Promise<T>, key: string): ApiState<T> {
  const [version, setVersion] = useState(0)
  const [loaded, setLoaded] = useState<Loaded<T>>({ requestKey: null, data: null, error: null })
  const requestKey = `${key}#${version}`
  const run = useEffectEvent((signal: AbortSignal) => load(signal))

  useEffect(() => {
    const controller = new AbortController()
    run(controller.signal)
      .then((data) => setLoaded({ requestKey, data, error: null }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        const apiError = error instanceof ApiError ? error : new ApiError(0, 'Une erreur est survenue.')
        setLoaded({ requestKey, data: null, error: apiError })
      })
    return () => controller.abort()
  }, [requestKey])

  const reload = useCallback(() => setVersion((v) => v + 1), [])

  // While a new request runs, the previous data stays visible.
  const loading = loaded.requestKey !== requestKey
  return { data: loaded.data, error: loading ? null : loaded.error, loading, reload }
}
