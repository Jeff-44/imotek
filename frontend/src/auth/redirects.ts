import type { User } from '../api/types'

/** Pages a user may be sent back to after signing in (others fall back to their own home page). */
export function canOpen(path: string, user: User): boolean {
  if (path.startsWith('/admin')) return user.roles.includes('Admin')
  if (path.startsWith('/espace-agent')) return user.roles.includes('Agent')
  return true
}

/** Where a user lands after signing in when no page was requested. */
export function defaultPageFor(user: User): string {
  if (user.roles.includes('Admin')) return '/admin'
  if (user.roles.includes('Agent')) return '/espace-agent'
  if (user.agentStatus === 'Pending') return '/compte' // shows where the request stands
  return '/biens'
}
