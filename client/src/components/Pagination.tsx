interface Props {
  page: number
  pageSize: number
  totalCount: number
  onPage: (page: number) => void
}

/** "Showing 21-40 of 95" plus Previous / Next. Used under every list. */
export function Pagination({ page, pageSize, totalCount, onPage }: Props) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)

  return (
    <div className="d-flex justify-content-between align-items-center">
      <span className="text-secondary small">
        {totalCount === 0 ? 'No results' : `Showing ${from}–${to} of ${totalCount}`}
      </span>
      <div className="btn-group">
        <button type="button" className="btn btn-outline-secondary btn-sm" disabled={page <= 1}
          onClick={() => onPage(page - 1)}>Previous</button>
        <span className="btn btn-sm disabled">Page {page} / {totalPages}</span>
        <button type="button" className="btn btn-outline-secondary btn-sm" disabled={page >= totalPages}
          onClick={() => onPage(page + 1)}>Next</button>
      </div>
    </div>
  )
}
