import { useState } from 'react'
import { Link, NavLink, useNavigate } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { Logo } from './Logo'

export function Header() {
  const [open, setOpen] = useState(false)
  const { user, hasRole, logout } = useAuth()
  const navigate = useNavigate()
  const close = () => setOpen(false)

  function handleLogout() {
    logout()
    close()
    navigate('/')
  }

  return (
    <header className="header">
      <div className="container header__inner">
        <Link to="/" className="header__brand" aria-label="Imotek — accueil" onClick={close}>
          <Logo />
        </Link>

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
            <li>
              <NavLink to="/biens" className="nav__link" onClick={close}>
                Biens disponibles
              </NavLink>
            </li>
            {hasRole('Agent') && (
              <li>
                <NavLink to="/espace-agent" className="nav__link" onClick={close}>
                  Espace agent
                </NavLink>
              </li>
            )}
            {user ? (
              <li className="nav__user">
                <span className="nav__name" title={user.email}>
                  {user.fullName}
                </span>
                <button type="button" className="nav__link link-button" onClick={handleLogout}>
                  Déconnexion
                </button>
              </li>
            ) : (
              <>
                <li>
                  <NavLink to="/connexion" className="nav__link" onClick={close}>
                    Connexion
                  </NavLink>
                </li>
                <li>
                  <NavLink to="/inscription" className="nav__link nav__link--cta" onClick={close}>
                    Créer un compte
                  </NavLink>
                </li>
              </>
            )}
          </ul>
        </nav>
      </div>
    </header>
  )
}
