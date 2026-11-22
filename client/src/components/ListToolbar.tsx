import type { ReactNode } from 'react'

interface Props {
  /** The search text currently in the URL. */
  q: string
  onSearch: (q: string) => void
  placeholder?: string
  /** Filter selects / chips go here. */
  children?: ReactNode
  /** Export buttons go here (see ExportButtons). */
  exportSlot?: ReactNode
}

/** Search box + filters + export slot above every list. The search runs on Enter / the Search button. */
export function ListToolbar({ q, onSearch, placeholder = 'Search...', children, exportSlot }: Props) {
  return (
    // key={q}: when the URL changes (Back button, a pasted link) React rebuilds the form, so the box shows the new text.
    <div className="d-flex flex-wrap gap-2 align-items-center mb-3">
      <form key={q} className="d-flex gap-2" role="search" onSubmit={(e) => {
        e.preventDefault()
        onSearch(String(new FormData(e.currentTarget).get('q') ?? ''))
      }}>
        <input name="q" className="form-control" type="search" aria-label="Search" placeholder={placeholder} defaultValue={q} />
        <button className="btn btn-outline-primary" type="submit">Search</button>
      </form>
      {children}
      {exportSlot && <div className="ms-auto">{exportSlot}</div>}
    </div>
  )
}
