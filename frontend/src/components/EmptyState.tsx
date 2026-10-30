import type { ReactNode } from 'react'

type EmptyStateProps = {
  title: string
  text: string
  action?: ReactNode
}

export function EmptyState({ title, text, action }: EmptyStateProps) {
  return (
    <div className="empty-state">
      <svg className="empty-state__icon" viewBox="0 0 48 48" width="48" height="48" aria-hidden="true">
        <path
          d="M8 22 24 9l16 13M12 19v19h24V19"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.5"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        <path d="M20 30h8" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
      </svg>
      <p className="empty-state__title">{title}</p>
      <p className="empty-state__text">{text}</p>
      {action}
    </div>
  )
}
