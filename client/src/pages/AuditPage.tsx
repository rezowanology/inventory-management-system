import { Fragment, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { auditApi, configApi } from '../api/endpoints'
import type { AuditRow } from '../api/types'
import { ErrorAlert } from '../components/Alerts'
import { ExportButtons } from '../components/ExportButtons'
import { ListToolbar } from '../components/ListToolbar'
import { Pagination } from '../components/Pagination'
import { toQueryString, useListParams } from '../hooks/useListParams'
import { formatDateTime } from '../lib/dates'

const PAGE_SIZE = 20
const FILTER_KEYS = ['actorId', 'action', 'entityType', 'from', 'to'] as const

function show(value: unknown): string {
  if (value === undefined) return ''
  return typeof value === 'string' ? value : JSON.stringify(value)
}

/** Old and new values side by side. Only the properties that changed are stored, so the table is short. */
function ValuesComparison({ id }: { id: number }) {
  const detail = useQuery({ queryKey: ['audit', 'detail', id], queryFn: () => auditApi.get(id) })
  if (detail.isPending) return <span className="text-secondary">Loading...</span>
  if (detail.isError) return <ErrorAlert error={detail.error} />

  const { oldValues, newValues } = detail.data
  const keys = [...new Set([...Object.keys(oldValues ?? {}), ...Object.keys(newValues ?? {})])]
  if (keys.length === 0) return <span className="text-secondary">No values are stored for this action (passwords are never logged).</span>

  return (
    <table className="table table-sm table-bordered mb-0 w-auto bg-body">
      <thead><tr><th>Property</th><th>Old value</th><th>New value</th></tr></thead>
      <tbody>
        {keys.map((k) => (
          <tr key={k}>
            <td><code>{k}</code></td>
            <td className="text-danger">{show(oldValues?.[k])}</td>
            <td className="text-success">{show(newValues?.[k])}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/** /audit (Admin): who changed what and when, plus logins. Click "Show" on a row to see old vs new values. */
export function AuditPage() {
  const { params, setQ, setFilter, setPage } = useListParams(FILTER_KEYS)
  const f = params.filters
  const [open, setOpen] = useState<Set<number>>(new Set())

  const query = toQueryString({ q: params.q, page: params.page, pageSize: PAGE_SIZE, ...f })
  const list = useQuery({ queryKey: ['audit', 'list', query], queryFn: () => auditApi.list(query) })
  const options = useQuery({ queryKey: ['audit', 'options'], queryFn: auditApi.filterOptions })
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  const timeZone = config.data?.timeZone ?? 'UTC'

  function toggle(row: AuditRow) {
    setOpen((current) => {
      const next = new Set(current)
      if (!next.delete(row.id)) next.add(row.id)
      return next
    })
  }

  return (
    <>
      <h1 className="h4 mb-3">Audit log</h1>
      <ErrorAlert error={list.error} />
      <ListToolbar q={params.q} onSearch={setQ} placeholder="Search entity, action or entity id..."
        exportSlot={<ExportButtons path="/api/audit" query={query} />}>
        <select className="form-select w-auto" aria-label="Actor filter" value={f.actorId} onChange={(e) => setFilter('actorId', e.target.value)}>
          <option value="">All users</option>
          {(options.data?.actors ?? []).map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
        </select>
        <select className="form-select w-auto" aria-label="Action filter" value={f.action} onChange={(e) => setFilter('action', e.target.value)}>
          <option value="">All actions</option>
          {(options.data?.actions ?? []).map((a) => <option key={a} value={a}>{a}</option>)}
        </select>
        <select className="form-select w-auto" aria-label="Entity type filter" value={f.entityType} onChange={(e) => setFilter('entityType', e.target.value)}>
          <option value="">All entity types</option>
          {(options.data?.entityTypes ?? []).map((a) => <option key={a} value={a}>{a}</option>)}
        </select>
        <label className="d-flex align-items-center gap-1 small">From
          <input type="date" className="form-control w-auto" aria-label="From date" value={f.from} onChange={(e) => setFilter('from', e.target.value)} />
        </label>
        <label className="d-flex align-items-center gap-1 small">To
          <input type="date" className="form-control w-auto" aria-label="To date" value={f.to} onChange={(e) => setFilter('to', e.target.value)} />
        </label>
      </ListToolbar>

      <div className="table-responsive">
        <table className="table table-striped table-sm align-middle">
          <thead><tr><th>Time</th><th>Actor</th><th>Action</th><th>Entity</th><th>IP address</th><th /></tr></thead>
          <tbody>
            {list.isPending && <tr><td colSpan={6} className="text-center text-secondary py-4">Loading...</td></tr>}
            {list.data?.items.map((row) => (
              <Fragment key={row.id}>
                <tr>
                  <td className="text-nowrap">{formatDateTime(row.createdAt, timeZone)}</td>
                  <td>{row.actor}</td>
                  <td><span className="badge text-bg-secondary">{row.action}</span></td>
                  <td>{row.entityType} <strong>{row.entityRepr}</strong> <span className="text-secondary small">#{row.entityId}</span></td>
                  <td className="text-secondary small">{row.ipAddress}</td>
                  <td className="text-end">
                    {row.hasValues && (
                      <button type="button" className="btn btn-sm btn-outline-secondary" aria-expanded={open.has(row.id)} onClick={() => toggle(row)}>
                        {open.has(row.id) ? 'Hide' : 'Show'} values
                      </button>
                    )}
                  </td>
                </tr>
                {open.has(row.id) && (
                  <tr><td colSpan={6} className="bg-body-tertiary"><ValuesComparison id={row.id} /></td></tr>
                )}
              </Fragment>
            ))}
            {list.data?.items.length === 0 && <tr><td colSpan={6} className="text-center text-secondary py-4">No audit rows match.</td></tr>}
          </tbody>
        </table>
      </div>
      <Pagination page={params.page} pageSize={PAGE_SIZE} totalCount={list.data?.totalCount ?? 0} onPage={setPage} />
      <div className="text-secondary small mt-1">Times are shown in {timeZone}. "system" means the application itself (for example the seeder).</div>
    </>
  )
}
