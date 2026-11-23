/**
 * The link a download button points at: the SAME list endpoint and the SAME filters as the screen, plus format=csv|xlsx.
 * (The server ignores paging for files, so every matching row is exported.) Pure function, so it is easy to test.
 */
export function exportUrl(path: string, query: string, format: 'csv' | 'xlsx'): string {
  return `${path}?${query}${query ? '&' : ''}format=${format}`
}

/**
 * CSV / Excel buttons. They are ordinary links: the browser downloads the file itself and sends the sign-in cookie
 * (same origin), so no fetch() is needed. `disabled` greys them out (e.g. while the date range is invalid).
 */
export function ExportButtons({ path, query, disabled = false }: { path: string; query: string; disabled?: boolean }) {
  const link = (format: 'csv' | 'xlsx', label: string) => (
    <a className={`btn btn-outline-secondary ${disabled ? 'disabled' : ''}`} href={exportUrl(path, query, format)}
      download aria-disabled={disabled} tabIndex={disabled ? -1 : undefined}>{label}</a>
  )
  return (
    <div className="btn-group btn-group-sm">
      {link('csv', 'Export CSV')}
      {link('xlsx', 'Export Excel')}
    </div>
  )
}
