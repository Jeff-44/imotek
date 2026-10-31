import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, useLocation, useNavigate } from 'react-router'
import { notificationsApi } from '../api/requests'
import { useAuth } from '../auth/useAuth'
import { Logo } from './Logo'

export function Header() {
  const [open, setOpen] = useState(false)
  const { user, hasRole, logout } = useAuth()
  const navigate = useNavigate()
  const close = () => setOpen(false)
  const unread = useUnreadCount(user?.id ?? null)
  const { pathname } = useLocation()
  const loggingOut = useRef(false)

  // Sign out only once the home page is shown. Signing out in the same click lets a protected
  // page redirect to the login page first, remembering itself as "from": the next person to sign
  // in on this browser would be sent to it.
  useEffect(() => {
    if (loggingOut.current && pathname === '/') {
      loggingOut.current = false
      logout()
    }
  }, [pathname, logout])

  function handleLogout() {
    close()
    if (pathname === '/') {
      logout()
      return
    }
    loggingOut.current = true
    navigate('/', { replace: true })
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
            {hasRole('Agent') && (
              <li>
                <NavLink to="/espace-agent/demandes" className="nav__link" onClick={close}>
                  Demandes reçues
                </NavLink>
              </li>
            )}
            {hasRole('Client') && (
              <li>
                <NavLink to="/mes-demandes" className="nav__link" onClick={close}>
                  Mes demandes
                </NavLink>
              </li>
            )}
            {user && (
              <li>
                <NavLink to="/notifications" className="nav__link" onClick={close}>
                  Notifications
                  {unread > 0 && (
                    <span className="nav__badge" aria-label={`${unread} non lues`}>
                      {unread}
                    </span>
                  )}
                </NavLink>
              </li>
            )}
            {hasRole('Admin') && (
              <li>
                <NavLink to="/admin" className="nav__link" onClick={close}>
                  Administration
                </NavLink>
              </li>
            )}
            {user ? (
              <li className="nav__user">
                <NavLink to="/compte" className="nav__name" title={`Mon compte (${user.email})`} onClick={close}>
                  {user.fullName}
                </NavLink>
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

const REFRESH_MS = 60_000

/** Unread notifications of the signed-in user, refreshed on navigation and every minute. */
function useUnreadCount(userId: string | null): number {
  const { pathname } = useLocation()
  const [count, setCount] = useState<{ userId: string | null; value: number }>({ userId: null, value: 0 })

  useEffect(() => {
    if (!userId) return
    const controller = new AbortController()
    const load = () =>
      notificationsApi
        .list(controller.signal)
        .then((list) => setCount({ userId, value: list.unreadCount }))
        .catch(() => undefined)
    load()
    const timer = window.setInterval(load, REFRESH_MS)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [userId, pathname])

  return count.userId === userId ? count.value : 0
}
