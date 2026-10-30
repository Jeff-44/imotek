import { apiFetch, toQueryString } from './client'
import type { PagedResult, Property, PropertyInput, PropertySearch } from './types'

export const propertiesApi = {
  search: (filters: PropertySearch, signal?: AbortSignal) =>
    apiFetch<PagedResult<Property>>(`/api/properties${toQueryString(filters)}`, { signal }),

  get: (id: string, signal?: AbortSignal) => apiFetch<Property>(`/api/properties/${encodeURIComponent(id)}`, { signal }),

  mine: (page: number, signal?: AbortSignal) =>
    apiFetch<PagedResult<Property>>(`/api/properties/my-properties${toQueryString({ page, pageSize: 50 })}`, { signal }),

  create: (input: PropertyInput) =>
    apiFetch<Property>('/api/properties', { method: 'POST', body: JSON.stringify(input) }),

  update: (id: string, input: PropertyInput) =>
    apiFetch<Property>(`/api/properties/${encodeURIComponent(id)}`, { method: 'PUT', body: JSON.stringify(input) }),

  remove: (id: string) => apiFetch<void>(`/api/properties/${encodeURIComponent(id)}`, { method: 'DELETE' }),
}
