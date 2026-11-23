import { useQuery } from '@tanstack/react-query'
import { ApiError } from '../../api/client'
import { historyApi, reportsApi } from '../../api/endpoints'
import type { SummaryDay } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { DataTable, type Column } from '../../components/DataTable'
import { ExportButtons } from '../../components/ExportButtons'
import { Pagination } from '../../components/Pagination'
import { toQueryString, useListParams } from '../../hooks/useListParams'
import { SummaryChart } from './SummaryChart'

const PAGE_SIZE = 15
const FILTER_KEYS = ['from', 'to', 'pointId', 'productId'] as const

function TotalCard({ label, value, tone }: { label: string; value: number | undefined; tone: string }) {
  return (
    <div className="col-6 col-md-3">
      <div className={`card border-${tone}`}>
        <div className="card-body py-2">
          <div className="small text-secondary">{label}</div>
          <div className="fs-3 fw-semibold" data-testid={`total-${label.toLowerCase()}`}>{value ?? '-'}</div>
        </div>
      </div>
    </div>
  )
}

/** /reports/summary: totals, a per-day chart and a per-day table for a date range (default: the last 30 days). */
export function SummaryReportPage() {
  const { params, setFilter, setPage } = useListParams(FILTER_KEYS)
  const f = params.filters

  // Empty from/to are left out of the URL; the server then uses "the last 30 days" and tells us which days it chose.
  const query = toQueryString({ page: params.page, pageSize: PAGE_SIZE, ...f })
  const report = useQuery({ queryKey: ['reports', 'summary', query], queryFn: () => reportsApi.summary(query) })
  const options = useQuery({ queryKey: ['history-filter-options'], queryFn: historyApi.filterOptions })

  const from = f.from || report.data?.from || ''
  const to = f.to || report.data?.to || ''
  const rangeError = report.error instanceof ApiError ? report.error.fieldErrors : {}

  const columns: Column<SummaryDay>[] = [
    { header: 'Date', cell: (d) => d.day },
    { header: 'Tracked', cell: (d) => d.tracked, className: 'text-end' },
    { header: 'Received', cell: (d) => d.received, className: 'text-end' },
    { header: 'Delivered', cell: (d) => d.delivered, className: 'text-end' },
    { header: 'Reversed', cell: (d) => d.reversed, className: 'text-end' },
  ]

  const totals = report.data?.totals
  return (
    <>
      <h1 className="h4 mb-3">Summary report</h1>

      <div className="d-flex flex-wrap gap-3 align-items-start mb-3">
        <div>
          <label htmlFor="sum-from" className="form-label small mb-0">From</label>
          <input id="sum-from" type="date" className={`form-control ${rangeError.from ? 'is-invalid' : ''}`} value={from}
            onChange={(e) => setFilter('from', e.target.value)} />
          <div className="invalid-feedback">{rangeError.from?.[0]}</div>
        </div>
        <div>
          <label htmlFor="sum-to" className="form-label small mb-0">To</label>
          <input id="sum-to" type="date" className={`form-control ${rangeError.to ? 'is-invalid' : ''}`} value={to}
            onChange={(e) => setFilter('to', e.target.value)} />
          <div className="invalid-feedback">{rangeError.to?.[0]}</div>
        </div>
        <div>
          <label htmlFor="sum-product" className="form-label small mb-0">Product</label>
          <select id="sum-product" className="form-select" value={f.productId} onChange={(e) => setFilter('productId', e.target.value)}>
            <option value="">All products</option>
            {(options.data?.products ?? []).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="sum-point" className="form-label small mb-0">Point</label>
          <select id="sum-point" className="form-select" value={f.pointId} onChange={(e) => setFilter('pointId', e.target.value)}>
            <option value="">All points</option>
            {(options.data?.points ?? []).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
          </select>
        </div>
        <div className="ms-auto align-self-end"><ExportButtons path="/api/reports/summary" query={query} disabled={!!report.error} /></div>
      </div>

      {/* A bad range shows under the date inputs; any other error in the red alert. */}
      {!(report.error instanceof ApiError && report.error.status === 422) && <ErrorAlert error={report.error} />}
      {report.error instanceof ApiError && report.error.status === 422 && <div className="alert alert-warning" role="alert">{report.error.detail}</div>}

      <div className="row g-3 mb-3">
        <TotalCard label="Tracked" value={totals?.tracked} tone="info" />
        <TotalCard label="Received" value={totals?.received} tone="success" />
        <TotalCard label="Delivered" value={totals?.delivered} tone="primary" />
        <TotalCard label="Reversed" value={totals?.reversed} tone="danger" />
      </div>

      {report.data && (
        <div className="card mb-3"><div className="card-body"><SummaryChart days={report.data.chart} /></div></div>
      )}

      <DataTable columns={columns} rows={report.data?.items} rowKey={(d) => d.day} loading={report.isPending} emptyText="No days in this range." />
      <Pagination page={params.page} pageSize={PAGE_SIZE} totalCount={report.data?.totalCount ?? 0} onPage={setPage} />
      <div className="text-secondary small mt-1">
        Days are local days in the configured time zone. "Tracked" counts one row per component scanned.
      </div>
    </>
  )
}
