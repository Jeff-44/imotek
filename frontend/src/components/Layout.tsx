import { useEffect } from 'react'
import { Outlet, useLocation } from 'react-router'
import { Header } from './Header'
import { LogoMark } from './Logo'

const currentYear = new Date().getFullYear()

export function Layout() {
  const { pathname } = useLocation()

  // Start each page at the top, like a regular page load.
  useEffect(() => {
    window.scrollTo(0, 0)
  }, [pathname])

  return (
    <>
      <a href="#main" className="skip-link">
        Aller au contenu
      </a>
      <Header />
      <main id="main">
        <Outlet />
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
