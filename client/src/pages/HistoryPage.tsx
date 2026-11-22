import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { configApi, historyApi } from '../api/endpoints'
import { ErrorAlert } from '../components/Alerts'
import { formatDateTime } from '../lib/dates'

const PAGE_SIZE = 20

export function HistoryPage() {
  // List state (the page number) lives in the URL so Back and shared links work.
  const [params, setParams] = useSearchParams()
  const page = Math.max(1, Number(params.get('page')) || 1)

  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  const history = useQuery({ queryKey: ['history', { page }], queryFn: () => historyApi.list(page, PAGE_SIZE) })

  const timeZone = config.data?.timeZone ?? 'UTC'
  const totalPages = history.data ? Math.max(1, Math.ceil(history.data.totalCount / PAGE_SIZE)) : 1

  return (
    <>
      <h1 className="h4 mb-3">Inventory history</h1>
      <ErrorAlert error={history.error} />
      <div className="table-responsive">
        <table className="table table-striped table-sm align-middle">
          <thead>
            <tr><th>Time</th><th>Barcode</th><th>Product</th><th>Event</th><th>Point</th><th>Component</th><th>By</th><th>Reason</th></tr>
          </thead>
          <tbody>
            {history.data?.items.map((r) => (
              <tr key={r.id}>
                <td>{formatDateTime(r.occurredAt, timeZone)}</td>
                <td><code>{r.unitBarcode}</code></td>
                <td>{r.productName}</td>
                <td><span className="badge text-bg-info">{r.eventType}</span></td>
                <td>{r.pointName}</td>
                <td>{r.componentName ? `${r.componentName} (${r.componentBarcode})` : ''}</td>
                <td>{r.performedByEmployeeNo} {r.performedByName}</td>
                <td>{r.reason}</td>
              </tr>
            ))}
            {history.data?.items.length === 0 && <tr><td colSpan={8} className="text-center text-secondary">No history yet.</td></tr>}
          </tbody>
        </table>
      </div>
      <div className="d-flex justify-content-between align-items-center">
        <span className="text-secondary small">{history.data?.totalCount ?? 0} rows; times shown in {timeZone}</span>
        <div className="btn-group">
          <button className="btn btn-outline-secondary btn-sm" disabled={page <= 1}
            onClick={() => setParams({ page: String(page - 1) })}>Previous</button>
          <span className="btn btn-sm disabled">Page {page} / {totalPages}</span>
          <button className="btn btn-outline-secondary btn-sm" disabled={page >= totalPages}
            onClick={() => setParams({ page: String(page + 1) })}>Next</button>
        </div>
      </div>
    </>
  )
}
