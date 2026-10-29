import { useState } from 'react'
import { Logo } from './Logo'

const navItems = [
  { href: '#recherche', label: 'Rechercher' },
  { href: '#services', label: 'Services' },
  { href: '#biens', label: 'Biens disponibles' },
]

export function Header() {
  const [open, setOpen] = useState(false)

  return (
    <header className="header">
      <div className="container header__inner">
        <a href="#top" className="header__brand" aria-label="Imotek — accueil">
          <Logo />
        </a>

        <button
          type="button"
          className="header__toggle"
          aria-expanded={open}
          aria-controls="main-nav"
          onClick={() => setOpen((value) => !value)}
        >
          <span className="sr-only">{open ? 'Fermer le menu' : 'Ouvrir le menu'}</span>
          <span className="header__toggle-bar" aria-hidden="true" />
        </button>

        <nav id="main-nav" className={open ? 'nav nav--open' : 'nav'} aria-label="Navigation principale">
          <ul className="nav__list">
            {navItems.map((item) => (
              <li key={item.href}>
                <a href={item.href} className="nav__link" onClick={() => setOpen(false)}>
                  {item.label}
                </a>
              </li>
            ))}
          </ul>
        </nav>
      </div>
    </header>
  )
}
