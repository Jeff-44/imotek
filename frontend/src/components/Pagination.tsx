type PaginationProps = {
  page: number
  totalPages: number
  onChange: (page: number) => void
}

export function Pagination({ page, totalPages, onChange }: PaginationProps) {
  if (totalPages <= 1) return null

  return (
    <nav className="pagination" aria-label="Pagination">
      <button type="button" className="button button--ghost" disabled={page <= 1} onClick={() => onChange(page - 1)}>
        Précédent
      </button>
      <span className="pagination__info">
        Page {page} sur {totalPages}
      </span>
      <button
        type="button"
        className="button button--ghost"
        disabled={page >= totalPages}
        onClick={() => onChange(page + 1)}
      >
        Suivant
      </button>
    </nav>
  )
}
