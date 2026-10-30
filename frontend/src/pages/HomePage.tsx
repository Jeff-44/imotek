import { useNavigate } from 'react-router'
import { AvailableProperties } from '../components/AvailableProperties'
import { SearchForm } from '../components/SearchForm'
import { Services } from '../components/Services'
import { toSearchParams } from '../searchParams'

export function HomePage() {
  const navigate = useNavigate()

  return (
    <>
      <section className="hero" aria-labelledby="hero-title">
        <div className="container hero__inner">
          <div className="hero__intro">
            <p className="hero__eyebrow">Immobilier en Haïti</p>
            <h1 id="hero-title" className="hero__title">
              Trouvez le bien qui vous ressemble, partout en Haïti
            </h1>
            <p className="hero__text">
              Location, achat ou mise en vente : Imotek réunit en un seul endroit les biens immobiliers du pays pour
              simplifier vos recherches.
            </p>
          </div>
          <SearchForm compact onSearch={(search) => navigate(`/biens?${toSearchParams(search)}`)} />
        </div>
      </section>

      <Services />
      <AvailableProperties />
    </>
  )
}
