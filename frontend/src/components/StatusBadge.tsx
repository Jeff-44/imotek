import { statusLabels, type PropertyStatus } from '../api/types'

export function StatusBadge({ status }: { status: PropertyStatus }) {
  return <span className={`status status--${status.toLowerCase()}`}>{statusLabels[status]}</span>
}
