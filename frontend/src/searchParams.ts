import {
  currencies,
  propertyTypes,
  transactionTypes,
  type Currency,
  type PropertySearch,
  type PropertyType,
  type TransactionType,
} from './api/types'

function oneOf<T extends string>(values: readonly T[], value: string | null): T | undefined {
  return values.find((v) => v === value)
}

function positiveNumber(value: string | null): number | undefined {
  if (!value) return undefined
  const number = Number(value)
  return Number.isFinite(number) && number >= 0 ? number : undefined
}

function text(value: string | null): string | undefined {
  const trimmed = value?.trim()
  return trimmed ? trimmed : undefined
}

/** Reads search criteria from the URL, dropping anything invalid. */
export function readSearch(params: URLSearchParams): PropertySearch {
  return {
    search: text(params.get('search')),
    location: text(params.get('location')),
    department: text(params.get('department')),
    propertyType: oneOf<PropertyType>(propertyTypes, params.get('propertyType')),
    transactionType: oneOf<TransactionType>(transactionTypes, params.get('transactionType')),
    minPrice: positiveNumber(params.get('minPrice')),
    maxPrice: positiveNumber(params.get('maxPrice')),
    currency: oneOf<Currency>(currencies, params.get('currency')),
    page: positiveNumber(params.get('page')) || undefined,
  }
}

export function toSearchParams(search: PropertySearch): URLSearchParams {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(search)) {
    if (value !== undefined && value !== '' && !(key === 'page' && value === 1)) params.set(key, String(value))
  }
  return params
}
