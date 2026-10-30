import { Link } from 'react-router'
import { EmptyState } from '../components/EmptyState'

export function NotFoundPage() {
  return (
    <div className="container page">
      <EmptyState
        title="Page introuvable"
        text="La page que vous cherchez n’existe pas."
        action={
          <Link to="/" className="button">
            Retour à l’accueil
          </Link>
        }
      />
    </div>
  )
}
