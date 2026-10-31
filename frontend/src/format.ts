import type { Currency, Property } from './api/types'

const formatters = new Map<Currency, Intl.NumberFormat>()

export function formatPrice(price: number, currency: Currency): string {
  let formatter = formatters.get(currency)
  if (!formatter) {
    formatter = new Intl.NumberFormat('fr-HT', { style: 'currency', currency, maximumFractionDigits: 0 })
    formatters.set(currency, formatter)
  }
  return formatter.format(price)
}

export function formatPropertyPrice(property: Pick<Property, 'price' | 'currency' | 'transactionType'>): string {
  const price = formatPrice(property.price, property.currency)
  return property.transactionType === 'Rent' ? `${price} / mois` : price
}

export function formatLocation(property: Pick<Property, 'city' | 'department'>): string {
  return `${property.city}, ${property.department}`
}

const dateTimeFormat = new Intl.DateTimeFormat('fr-HT', { dateStyle: 'medium', timeStyle: 'short' })

export function formatDateTime(value: string): string {
  return dateTimeFormat.format(new Date(value))
}

const dateFormat = new Intl.DateTimeFormat('fr-HT', { dateStyle: 'medium' })

/** A calendar date (yyyy-mm-dd), shown as such whatever the time zone. */
export function formatDate(value: string): string {
  const [year, month, day] = value.split('-').map(Number)
  return dateFormat.format(new Date(year, month - 1, day))
}
