import './App.css'
import { AvailableProperties } from './components/AvailableProperties'
import { Header } from './components/Header'
import { LogoMark } from './components/Logo'
import { SearchForm } from './components/SearchForm'
import { Services } from './components/Services'

const currentYear = new Date().getFullYear()

function App() {
  return (
    <>
      <a href="#main" className="skip-link">
        Aller au contenu
      </a>
      <Header />

      <main id="main">
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
            <SearchForm />
          </div>
        </section>

        <Services />

        {/* Listings will come from the API once the property endpoints exist. */}
        <AvailableProperties properties={[]} />
      </main>

      <footer className="footer">
        <div className="container footer__inner">
          <span className="footer__brand">
            <LogoMark size={24} />
            Imotek
          </span>
          <p className="footer__text">© {currentYear} Imotek · Immobilier en Haïti</p>
        </div>
      </footer>
    </>
  )
}

export default App
