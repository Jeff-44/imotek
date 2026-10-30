import { describe, expect, it } from 'vitest'
import { readSearch, searchProblem, toSearchParams } from './searchParams'

describe('readSearch', () => {
  it('keeps valid criteria and trims text', () => {
    const search = readSearch(
      new URLSearchParams('location=%20Jacmel%20&propertyType=Land&transactionType=Sale&minPrice=100&currency=USD&page=2'),
    )
    expect(search).toMatchObject({
      location: 'Jacmel',
      propertyType: 'Land',
      transactionType: 'Sale',
      minPrice: 100,
      currency: 'USD',
      page: 2,
    })
  })

  it('drops values outside the allowed lists', () => {
    const search = readSearch(new URLSearchParams('propertyType=Castle&transactionType=Lease&currency=EUR&minPrice=-5&maxPrice=abc'))
    expect(search.propertyType).toBeUndefined()
    expect(search.transactionType).toBeUndefined()
    expect(search.currency).toBeUndefined()
    expect(search.minPrice).toBeUndefined()
    expect(search.maxPrice).toBeUndefined()
  })
})

describe('toSearchParams', () => {
  it('omits empty values and the first page', () => {
    expect(toSearchParams({ location: 'Delmas', page: 1, search: undefined }).toString()).toBe('location=Delmas')
  })

  it('round-trips with readSearch', () => {
    const search = { location: 'Cap-Haïtien', transactionType: 'Rent' as const, maxPrice: 900, currency: 'HTG' as const, page: 3 }
    expect(readSearch(toSearchParams(search))).toMatchObject(search)
  })
})

describe('searchProblem', () => {
  it('requires a currency for a price range', () => {
    expect(searchProblem({ minPrice: 100 })).toMatch(/devise/)
    expect(searchProblem({ maxPrice: 100 })).toMatch(/devise/)
    expect(searchProblem({ minPrice: 100, currency: 'HTG' })).toBeNull()
  })

  it('rejects an inverted range', () => {
    expect(searchProblem({ minPrice: 500, maxPrice: 100, currency: 'USD' })).toMatch(/minimum/)
  })

  it('accepts a currency alone or no price at all', () => {
    expect(searchProblem({ currency: 'USD' })).toBeNull()
    expect(searchProblem({ location: 'Jérémie' })).toBeNull()
  })
})
