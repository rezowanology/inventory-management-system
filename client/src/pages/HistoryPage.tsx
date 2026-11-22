import { useQuery } from '@tanstack/react-query'
import { configApi, historyApi } from '../api/endpoints'
import type { HistoryRow } from '../api/types'
import { ErrorAlert } from '../components/Alerts'
import { DataTable, type Column } from '../components/DataTable'
import { ListToolbar } from '../components/ListToolbar'
import { Pagination } from '../components/Pagination'
import { toQueryString, useListParams } from '../hooks/useListParams'
import { formatDateTime } from '../lib/dates'

const PAGE_SIZE = 20
const FILTER_KEYS = ['eventType', 'pointId', 'productId', 'userId', 'from', 'to'] as const
const EVENT_TYPES = ['COMPONENT_TRACKED', 'RECEIVED', 'DELIVERED', 'REVERSED']
const EVENT_STYLE: Record<string, string> = {
  COMPONENT_TRACKED: 'text-bg-info', RECEIVED: 'text-bg-success', DELIVERED: 'text-bg-primary', REVERSED: 'text-bg-danger',
}

/** /history: every inventory row, newest first, with filters (all kept in the URL) and paging. */
export function HistoryPage() {
  const { params, setQ, setFilter, setPage } = useListParams(FILTER_KEYS)
  const f = params.filters

  const query = toQueryString({ q: params.q, page: params.page, pageSize: PAGE_SIZE, ...f })
  const history = useQuery({ queryKey: ['history', query], queryFn: () => historyApi.list(query) })
  const options = useQuery({ queryKey: ['history-filter-options'], queryFn: historyApi.filterOptions })
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  const timeZone = config.data?.timeZone ?? 'UTC'

  const columns: Column<HistoryRow>[] = [
    { header: 'Time', cell: (r) => formatDateTime(r.occurredAt, timeZone), className: 'text-nowrap' },
    { header: 'Barcode', cell: (r) => <code>{r.unitBarcode}</code> },
    { header: 'Product', cell: (r) => r.productName },
    {
      header: 'Event',
      // Day 4 adds a "reversed" badge on rows that an admin has cancelled; REVERSED rows already have their own colour.
      cell: (r) => <span className={`badge ${EVENT_STYLE[r.eventType] ?? 'text-bg-secondary'}`}>{r.eventType}</span>,
    },
    { header: 'Point', cell: (r) => r.pointName },
    { header: 'Component', cell: (r) => (r.componentName ? `${r.componentName} (${r.componentBarcode})` : '') },
    { header: 'By', cell: (r) => `${r.performedByEmployeeNo} ${r.performedByName}` },
    { header: 'Reason', cell: (r) => r.reason },
  ]

  const select = (key: (typeof FILTER_KEYS)[number], label: string, items: { value: string; label: string }[]) => (
    <select className="form-select w-auto" aria-label={label} value={f[key]} onChange={(e) => setFilter(key, e.target.value)}>
      <option value="">{label}</option>
      {items.map((i) => <option key={i.value} value={i.value}>{i.label}</option>)}
    </select>
  )

  return (
    <>
      <h1 className="h4 mb-3">Inventory history</h1>
      <ErrorAlert error={history.error} />
      <ListToolbar q={params.q} onSearch={setQ} placeholder="Search barcode, component barcode or product...">
        {select('eventType', 'All events', EVENT_TYPES.map((t) => ({ value: t, label: t })))}
        {select('productId', 'All products', (options.data?.products ?? []).map((o) => ({ value: String(o.id), label: o.name })))}
        {select('pointId', 'All points', (options.data?.points ?? []).map((o) => ({ value: String(o.id), label: o.name })))}
        {select('userId', 'All users', (options.data?.users ?? []).map((o) => ({ value: String(o.id), label: o.name })))}
        <label className="d-flex align-items-center gap-1 small">From
          <input type="date" className="form-control w-auto" aria-label="From date" value={f.from} onChange={(e) => setFilter('from', e.target.value)} />
        </label>
        <label className="d-flex align-items-center gap-1 small">To
          <input type="date" className="form-control w-auto" aria-label="To date" value={f.to} onChange={(e) => setFilter('to', e.target.value)} />
        </label>
      </ListToolbar>

      <DataTable columns={columns} rows={history.data?.items} rowKey={(r) => r.id} loading={history.isPending}
        emptyText="No history rows match." />
      <Pagination page={params.page} pageSize={PAGE_SIZE} totalCount={history.data?.totalCount ?? 0} onPage={setPage} />
      <div className="text-secondary small mt-1">Times are shown in {timeZone}.</div>
    </>
  )
}
