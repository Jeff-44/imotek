import { apiFetch, toQueryString } from './client'
import type { PagedResult, Property, PropertyInput, PropertySearch } from './types'

const path = (id: string) => `/api/properties/${encodeURIComponent(id)}`

export const propertiesApi = {
  search: (filters: PropertySearch, signal?: AbortSignal) =>
    apiFetch<PagedResult<Property>>(`/api/properties${toQueryString(filters)}`, { signal }),

  get: (id: string, signal?: AbortSignal) => apiFetch<Property>(path(id), { signal }),

  mine: (page: number, signal?: AbortSignal) =>
    apiFetch<PagedResult<Property>>(`/api/properties/my-properties${toQueryString({ page, pageSize: 50 })}`, { signal }),

  create: (input: PropertyInput) =>
    apiFetch<Property>('/api/properties', { method: 'POST', body: JSON.stringify(input) }),

  update: (id: string, input: PropertyInput) =>
    apiFetch<Property>(path(id), { method: 'PUT', body: JSON.stringify(input) }),

  remove: (id: string) => apiFetch<void>(path(id), { method: 'DELETE' }),

  uploadImage: (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return apiFetch<Property>(`${path(id)}/images`, { method: 'POST', body: form })
  },

  deleteImage: (id: string, imageId: string) =>
    apiFetch<Property>(`${path(id)}/images/${encodeURIComponent(imageId)}`, { method: 'DELETE' }),
}
