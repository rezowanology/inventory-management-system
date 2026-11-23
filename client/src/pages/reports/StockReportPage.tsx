import { useQuery } from '@tanstack/react-query'
import { configApi, historyApi, reportsApi } from '../../api/endpoints'
import type { StockRow } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { DataTable, type Column } from '../../components/DataTable'
import { ExportButtons } from '../../components/ExportButtons'
import { ListToolbar } from '../../components/ListToolbar'
import { Pagination } from '../../components/Pagination'
import { toQueryString, useListParams } from '../../hooks/useListParams'
import { formatDateTime } from '../../lib/dates'

const PAGE_SIZE = 20
const FILTER_KEYS = ['productId', 'pointId', 'minDays', 'sort'] as const

/** /reports/stock: one row per unit that is in stock right now (derived from history), with per-receive-point chips. */
export function StockReportPage() {
  const { params, setQ, setFilter, setPage } = useListParams(FILTER_KEYS)
  const f = params.filters

  const query = toQueryString({ q: params.q, page: params.page, pageSize: PAGE_SIZE, ...f })
  // Keys start with 'reports' so a scan or a reversal (which invalidates ['reports']) refreshes this page too.
  const report = useQuery({ queryKey: ['reports', 'stock', query], queryFn: () => reportsApi.stock(query) })
  const options = useQuery({ queryKey: ['history-filter-options'], queryFn: historyApi.filterOptions })
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  const timeZone = config.data?.timeZone ?? 'UTC'

  const columns: Column<StockRow>[] = [
    { header: 'Barcode', cell: (r) => <code>{r.barcode}</code> },
    { header: 'Product', cell: (r) => r.productName },
    { header: 'Receive point', cell: (r) => r.pointName },
    { header: 'Received at', cell: (r) => formatDateTime(r.receivedAt, timeZone), className: 'text-nowrap' },
    { header: 'Received by', cell: (r) => r.receivedBy },
    { header: 'Days in stock', cell: (r) => r.daysInStock, className: 'text-end' },
    { header: 'Components', cell: (r) => r.componentCount, className: 'text-end' },
  ]

  const byPoint = report.data?.byPoint ?? []
  const totalInChips = byPoint.reduce((sum, p) => sum + p.count, 0)

  return (
    <>
      <h1 className="h4 mb-3">Stock report</h1>
      <ErrorAlert error={report.error} />

      {/* One chip per receive point; clicking a chip filters the table to that point. */}
      <div className="d-flex flex-wrap gap-2 mb-3" aria-label="Stock per receive point">
        <button type="button" className={`btn btn-sm ${f.pointId ? 'btn-outline-secondary' : 'btn-secondary'}`} onClick={() => setFilter('pointId', '')}>
          All points <span className="badge text-bg-light">{totalInChips}</span>
        </button>
        {byPoint.map((p) => (
          <button key={p.pointId} type="button" onClick={() => setFilter('pointId', String(p.pointId))}
            className={`btn btn-sm ${f.pointId === String(p.pointId) ? 'btn-primary' : 'btn-outline-primary'}`}>
            {p.pointName} <span className="badge text-bg-light">{p.count}</span>
          </button>
        ))}
      </div>

      <ListToolbar q={params.q} onSearch={setQ} placeholder="Search barcode or product..."
        exportSlot={<ExportButtons path="/api/reports/stock" query={query} />}>
        <select className="form-select w-auto" aria-label="Product filter" value={f.productId} onChange={(e) => setFilter('productId', e.target.value)}>
          <option value="">All products</option>
          {(options.data?.products ?? []).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
        </select>
        <label className="d-flex align-items-center gap-1 small">In stock at least
          <input type="number" min={0} className="form-control w-auto" style={{ width: '6rem' }} aria-label="Minimum days in stock"
            value={f.minDays} onChange={(e) => setFilter('minDays', e.target.value)} /> days
        </label>
        <select className="form-select w-auto" aria-label="Sort" value={f.sort} onChange={(e) => setFilter('sort', e.target.value)}>
          <option value="">Longest in stock first</option>
          <option value="days">Shortest in stock first</option>
          <option value="barcode">Barcode A-Z</option>
        </select>
      </ListToolbar>

      <DataTable columns={columns} rows={report.data?.items} rowKey={(r) => r.barcode} loading={report.isPending}
        emptyText="No units are in stock with these filters." />
      <Pagination page={params.page} pageSize={PAGE_SIZE} totalCount={report.data?.totalCount ?? 0} onPage={setPage} />
      <div className="text-secondary small mt-1">Days in stock are counted from the last receive. Times are shown in {timeZone}.</div>
    </>
  )
}
