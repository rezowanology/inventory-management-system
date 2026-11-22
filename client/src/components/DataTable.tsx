import type { ReactNode } from 'react'

export interface Column<T> {
  header: string
  /** Draws the cell for one row. */
  cell: (row: T) => ReactNode
  className?: string
}

interface Props<T> {
  columns: Column<T>[]
  rows: T[] | undefined
  rowKey: (row: T) => string | number
  loading?: boolean
  emptyText?: string
  /** Optional extra CSS class for a row (e.g. grey out archived rows). */
  rowClass?: (row: T) => string | undefined
}

/** One table for every list screen: you describe the columns, it draws header, rows, loading and empty states. */
export function DataTable<T>({ columns, rows, rowKey, loading, emptyText = 'Nothing found.', rowClass }: Props<T>) {
  return (
    <div className="table-responsive">
      <table className="table table-striped table-sm align-middle">
        <thead>
          <tr>{columns.map((c) => <th key={c.header} className={c.className}>{c.header}</th>)}</tr>
        </thead>
        <tbody>
          {loading && !rows && (
            <tr><td colSpan={columns.length} className="text-center text-secondary py-4">Loading...</td></tr>
          )}
          {rows?.map((row) => (
            <tr key={rowKey(row)} className={rowClass?.(row)}>
              {columns.map((c) => <td key={c.header} className={c.className}>{c.cell(row)}</td>)}
            </tr>
          ))}
          {rows?.length === 0 && (
            <tr><td colSpan={columns.length} className="text-center text-secondary py-4">{emptyText}</td></tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
