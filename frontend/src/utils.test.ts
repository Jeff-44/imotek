import { describe, expect, it } from 'vitest'
import { translateError, translateFieldErrors } from './api/errorMessages'
import { MAX_UPLOAD_BYTES } from './api/types'
import { formatLocation, formatPropertyPrice } from './format'
import { checkImageFile } from './imageFiles'

describe('translateError', () => {
  it.each([
    ['Title is required.', 'Ce champ est obligatoire.'],
    ['Title must be at most 150 characters.', '150 caractères au maximum.'],
    ['Currency is required when filtering by price.', 'Choisissez une devise pour filtrer par prix.'],
    ['Only JPEG, PNG and WebP images are accepted.', 'Seules les images JPEG, PNG et WebP sont acceptées.'],
    ['The file must be at most 5 MB.', 'Le fichier ne doit pas dépasser 5 Mo.'],
    ['Invalid email or password.', 'Adresse e-mail ou mot de passe incorrect.'],
    ['You can only manage your own properties.', 'Vous ne pouvez gérer que vos propres biens.'],
  ])('translates "%s"', (message, expected) => {
    expect(translateError(message)).toBe(expected)
  })

  it('keeps unknown messages unchanged', () => {
    expect(translateError('Something new')).toBe('Something new')
  })
})

describe('translateFieldErrors', () => {
  it('normalizes JSON paths and PascalCase keys to camelCase and merges duplicates', () => {
    expect(
      translateFieldErrors({
        '$.price': ['The JSON value could not be converted to System.Decimal.'],
        Price: ['Price must be greater than 0.'],
        Title: ['Title is required.'],
      }),
    ).toEqual({
      price: ['Valeur invalide.', 'Le prix doit être supérieur à 0.'],
      title: ['Ce champ est obligatoire.'],
    })
  })
})

describe('format', () => {
  it('shows a monthly price for rentals only', () => {
    expect(formatPropertyPrice({ price: 1200, currency: 'USD', transactionType: 'Rent' })).toMatch(/1\s?200.*\/ mois$/)
    expect(formatPropertyPrice({ price: 45000, currency: 'HTG', transactionType: 'Sale' })).not.toMatch(/mois/)
  })

  it('formats the location', () => {
    expect(formatLocation({ city: 'Jacmel', department: 'Sud-Est' })).toBe('Jacmel, Sud-Est')
  })
})

describe('checkImageFile', () => {
  it('accepts supported images within the size limit', () => {
    expect(checkImageFile({ name: 'a.jpg', type: 'image/jpeg', size: 1000 })).toBeNull()
    expect(checkImageFile({ name: 'a.webp', type: 'image/webp', size: MAX_UPLOAD_BYTES })).toBeNull()
  })

  it('rejects other types and large files', () => {
    expect(checkImageFile({ name: 'a.svg', type: 'image/svg+xml', size: 10 })).toMatch(/JPEG, PNG et WebP/)
    expect(checkImageFile({ name: 'a.gif', type: 'image/gif', size: 10 })).toMatch(/JPEG, PNG et WebP/)
    expect(checkImageFile({ name: 'big.png', type: 'image/png', size: MAX_UPLOAD_BYTES + 1 })).toMatch(/Mo/)
  })
})
