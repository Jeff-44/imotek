import { useState, type FormEvent } from 'react'
import {
  currencies,
  departments,
  propertyTypeLabels,
  propertyTypes,
  type PropertySearch,
} from '../api/types'
import { readSearch, searchProblem } from '../searchParams'

type SearchFormProps = {
  /** Compact: transaction, location and type only (home page). */
  compact?: boolean
  initial?: PropertySearch
  onSearch: (search: PropertySearch) => void
}

export function SearchForm({ compact = false, initial = {}, onSearch }: SearchFormProps) {
  const [problem, setProblem] = useState<string | null>(null)

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const params = new URLSearchParams()
    for (const [key, value] of data) if (typeof value === 'string') params.set(key, value)
    const search = readSearch(params)
    const issue = searchProblem(search)
    setProblem(issue)
    if (!issue) onSearch(search)
  }

  // Remount when the criteria change (back/forward navigation) so the fields show them.
  const formKey = JSON.stringify(initial)

  return (
    <form key={formKey} className="search" onSubmit={handleSubmit} aria-labelledby="search-title" role="search">
      <h2 id="search-title" className="search__title">
        Rechercher un bien
      </h2>

      <fieldset className="search__field">
        <legend className="search__label">Transaction</legend>
        <div className="segmented segmented--three">
          {[
            { value: '', label: 'Tous' },
            { value: 'Rent', label: 'Louer' },
            { value: 'Sale', label: 'Acheter' },
          ].map((option) => (
            <label key={option.value} className="segmented__option">
              <input
                type="radio"
                name="transactionType"
                value={option.value}
                defaultChecked={(initial.transactionType ?? '') === option.value}
              />
              <span>{option.label}</span>
            </label>
          ))}
        </div>
      </fieldset>

      <div className="search__field">
        <label htmlFor="search-location" className="search__label">
          Localité
        </label>
        <input
          id="search-location"
          name="location"
          type="text"
          className="input"
          placeholder="Ex. Pétion-Ville, Cap-Haïtien…"
          defaultValue={initial.location ?? ''}
        />
      </div>

      <div className="search__field">
        <label htmlFor="search-type" className="search__label">
          Type de bien
        </label>
        <select id="search-type" name="propertyType" className="input" defaultValue={initial.propertyType ?? ''}>
          <option value="">Tous les types</option>
          {propertyTypes.map((type) => (
            <option key={type} value={type}>
              {propertyTypeLabels[type]}
            </option>
          ))}
        </select>
      </div>

      {!compact && (
        <>
          <div className="search__field">
            <label htmlFor="search-department" className="search__label">
              Département
            </label>
            <select id="search-department" name="department" className="input" defaultValue={initial.department ?? ''}>
              <option value="">Tous les départements</option>
              {departments.map((department) => (
                <option key={department} value={department}>
                  {department}
                </option>
              ))}
            </select>
          </div>

          <div className="search__field">
            <span className="search__label" id="search-price-label">
              Prix
            </span>
            <div className="search__row" role="group" aria-labelledby="search-price-label">
              <input
                name="minPrice"
                type="number"
                min="0"
                inputMode="numeric"
                className="input"
                placeholder="Min"
                aria-label="Prix minimum"
                defaultValue={initial.minPrice ?? ''}
              />
              <input
                name="maxPrice"
                type="number"
                min="0"
                inputMode="numeric"
                className="input"
                placeholder="Max"
                aria-label="Prix maximum"
                defaultValue={initial.maxPrice ?? ''}
              />
              <select name="currency" className="input" aria-label="Devise" defaultValue={initial.currency ?? ''}>
                <option value="">Devise</option>
                {currencies.map((currency) => (
                  <option key={currency} value={currency}>
                    {currency}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="search__field">
            <label htmlFor="search-text" className="search__label">
              Mots-clés
            </label>
            <input
              id="search-text"
              name="search"
              type="search"
              className="input"
              placeholder="Ex. piscine, meublé…"
              defaultValue={initial.search ?? ''}
            />
          </div>
        </>
      )}

      {problem && (
        <p className="field__error" role="alert">
          {problem}
        </p>
      )}

      <button type="submit" className="button search__submit">
        Rechercher
      </button>
    </form>
  )
}
