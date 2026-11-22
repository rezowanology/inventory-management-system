import { useState } from 'react'
import { request } from '../api/client'
import { ErrorAlert } from './Alerts'

/**
 * CSV / Excel buttons. They already call the list endpoint with format=csv|xlsx and the same filters;
 * the API answers "501 Not implemented" until the real export is built on Day 4 and the message is shown here.
 */
export function ExportButtons({ path, query }: { path: string; query: string }) {
  const [error, setError] = useState<unknown>(null)

  async function run(format: 'csv' | 'xlsx') {
    setError(null)
    try { await request(`${path}?${query}${query ? '&' : ''}format=${format}`) } catch (e) { setError(e) }
  }

  return (
    <div className="d-flex flex-column align-items-end">
      <div className="btn-group btn-group-sm">
        <button type="button" className="btn btn-outline-secondary" onClick={() => run('csv')}>Export CSV</button>
        <button type="button" className="btn btn-outline-secondary" onClick={() => run('xlsx')}>Export Excel</button>
      </div>
      {error ? <div className="mt-2"><ErrorAlert error={error} /></div> : null}
    </div>
  )
}
