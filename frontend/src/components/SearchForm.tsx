import { useState, type FormEvent } from 'react'

const propertyTypes = ['Maison', 'Appartement', 'Terrain', 'Local commercial', 'Bureau']

export function SearchForm() {
  const [submitted, setSubmitted] = useState(false)

  // The search API does not exist yet: the form only tells the visitor so.
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
  }

  return (
    <form id="recherche" className="search" onSubmit={handleSubmit} aria-labelledby="search-title">
      <h2 id="search-title" className="search__title">
        Rechercher un bien
      </h2>

      <fieldset className="search__field search__field--transaction">
        <legend className="search__label">Transaction</legend>
        <div className="segmented">
          <label className="segmented__option">
            <input type="radio" name="transaction" value="louer" defaultChecked />
            <span>Louer</span>
          </label>
          <label className="segmented__option">
            <input type="radio" name="transaction" value="acheter" />
            <span>Acheter</span>
          </label>
        </div>
      </fieldset>

      <div className="search__field">
        <label htmlFor="search-location" className="search__label">
          Localité
        </label>
        <input
          id="search-location"
          name="localite"
          type="text"
          className="search__input"
          placeholder="Ex. Pétion-Ville, Cap-Haïtien…"
          autoComplete="off"
        />
      </div>

      <div className="search__field">
        <label htmlFor="search-type" className="search__label">
          Type de bien
        </label>
        <select id="search-type" name="type" className="search__input" defaultValue="">
          <option value="">Tous les types</option>
          {propertyTypes.map((type) => (
            <option key={type} value={type}>
              {type}
            </option>
          ))}
        </select>
      </div>

      <button type="submit" className="button search__submit">
        Rechercher
      </button>

      <p className="search__notice" role="status">
        {submitted
          ? 'La recherche en ligne n’est pas encore disponible. Elle sera activée dès la mise en service de notre catalogue.'
          : 'Aperçu : la recherche en ligne sera bientôt disponible.'}
      </p>
    </form>
  )
}
