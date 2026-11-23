import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { configApi, reportsApi } from '../../api/endpoints'
import type { HistoryRow } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { ExportButtons } from '../../components/ExportButtons'
import { StatusBadge } from '../../components/StatusBadge'
import { isValidBarcode, normalizeBarcode } from '../../lib/barcode'
import { formatDateTime } from '../../lib/dates'

const EVENT_STYLE: Record<string, string> = {
  COMPONENT_TRACKED: 'text-bg-info', RECEIVED: 'text-bg-success', DELIVERED: 'text-bg-primary', REVERSED: 'text-bg-danger',
}

function TimelineItem({ row, timeZone }: { row: HistoryRow; timeZone: string }) {
  const struck = row.reversed ? 'text-decoration-line-through text-secondary' : ''
  return (
    <li id={`row-${row.id}`} className="mb-3 position-relative">
      <span className={`position-absolute top-0 start-0 translate-middle rounded-circle border border-2 bg-body ${row.eventType === 'REVERSED' ? 'border-danger' : 'border-secondary'}`}
        style={{ width: 14, height: 14, marginLeft: -1 }} aria-hidden="true" />
      <div className="ps-3">
        <div className={struck}>
          <span className="text-nowrap">{formatDateTime(row.occurredAt, timeZone)}</span>{' '}
          <span className={`badge ${EVENT_STYLE[row.eventType] ?? 'text-bg-secondary'}`}>{row.eventType}</span>{' '}
          at {row.pointName}
          {row.componentName && <> - {row.componentName} (<code>{row.componentBarcode}</code>)</>}
        </div>
        <div className="small text-secondary">
          by {row.performedByEmployeeNo} {row.performedByName}
          {row.reversesId && <> - cancels row <a href={`#row-${row.reversesId}`}>#{row.reversesId}</a></>}
        </div>
        {row.reason && <div className="small">Reason: {row.reason}</div>}
        {row.reversed && row.reversedAt && (
          <div className="small text-danger">
            Reversed by {row.reversedBy} on {formatDateTime(row.reversedAt, timeZone)}
            {row.reversedById && <> (<a href={`#row-${row.reversedById}`}>reversal #{row.reversedById}</a>)</>}
          </div>
        )}
      </div>
    </li>
  )
}

/** /reports/barcode: enter a unit barcode OR a component barcode and see the unit's complete history, reversals included. */
export function BarcodeReportPage() {
  const [search, setSearch] = useSearchParams()
  const barcode = normalizeBarcode(search.get('barcode'))
  const valid = isValidBarcode(barcode)

  const report = useQuery({ queryKey: ['reports', 'barcode', barcode], queryFn: () => reportsApi.barcode(barcode), enabled: valid })
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  const timeZone = config.data?.timeZone ?? 'UTC'
  const data = report.data
  const unit = data?.unit
  // The timeline can be narrowed to one kind of event (filtering happens in the browser: a unit has only a handful of rows).
  const [eventFilter, setEventFilter] = useState('')
  const timeline = (data?.timeline ?? []).filter((r) => !eventFilter || r.eventType === eventFilter)

  return (
    <>
      <h1 className="h4 mb-3">Barcode history</h1>

      <form key={barcode} className="d-flex gap-2 mb-3" role="search" onSubmit={(e) => {
        e.preventDefault()
        const value = normalizeBarcode(String(new FormData(e.currentTarget).get('barcode') ?? ''))
        setSearch(value ? { barcode: value } : {})
      }}>
        <input name="barcode" className={`form-control ${search.get('barcode') && !valid ? 'is-invalid' : ''}`} autoFocus autoComplete="off"
          aria-label="Barcode" placeholder="Unit barcode or component barcode" defaultValue={barcode} />
        <button type="submit" className="btn btn-primary">Show history</button>
      </form>
      {search.get('barcode') && !valid && <div className="alert alert-warning">Barcode must be 6-32 characters: A-Z, 0-9 and "-".</div>}
      <ErrorAlert error={report.error} />

      {data && !data.found && <div className="alert alert-warning" role="status">No unit or component has the barcode <code>{data.query}</code>.</div>}

      {/* A component that was attached to several units over time: let the user choose which unit's history to see. */}
      {data && data.matches.length > 1 && (
        <div className="card mb-3">
          <div className="card-body">
            <p className="mb-2">Component <code>{data.query}</code> was attached to {data.matches.length} units. Which one?</p>
            <div className="list-group">
              {data.matches.map((m) => (
                <button key={m.barcode} type="button" className="list-group-item list-group-item-action d-flex gap-2 align-items-center"
                  onClick={() => setSearch({ barcode: m.barcode })}>
                  <code>{m.barcode}</code> <span>{m.productName}</span> <StatusBadge status={m.status} />
                </button>
              ))}
            </div>
          </div>
        </div>
      )}

      {unit && data && (
        <>
          <div className="card mb-3" data-testid="barcode-header">
            <div className="card-body">
              <div className="d-flex flex-wrap align-items-center gap-2">
                <code className="fs-5">{unit.barcode}</code>
                <StatusBadge status={unit.status} />
                <span>{unit.product}</span>
                {unit.currentPoint && unit.since && (
                  <span className="text-secondary small">
                    {unit.status === 'Delivered' ? 'delivered from' : 'at'} {unit.currentPoint}, {formatDateTime(unit.since, timeZone)}
                  </span>
                )}
                {data.query !== unit.barcode && <span className="badge text-bg-light">found via component {data.query}</span>}
                <div className="ms-auto"><ExportButtons path="/api/reports/barcode" query={`barcode=${encodeURIComponent(unit.barcode)}`} /></div>
              </div>
              <div className="small mt-2">
                <span className="text-secondary">Attached components now:</span>{' '}
                {unit.components.length === 0 ? 'none'
                  : unit.components.map((c) => `${c.componentName} (${c.componentBarcode})`).join(', ')}
              </div>
            </div>
          </div>

          <div className="d-flex align-items-center gap-3 mb-2">
            <h2 className="h6 text-secondary mb-0">
              {eventFilter ? `${timeline.length} of ${data.timeline.length} rows` : `All ${data.timeline.length} rows`}, oldest first
            </h2>
            <select className="form-select form-select-sm w-auto" aria-label="Event filter" value={eventFilter} onChange={(e) => setEventFilter(e.target.value)}>
              <option value="">All events</option>
              {Object.keys(EVENT_STYLE).map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <ol className="list-unstyled border-start ms-2 ps-2 mt-3">
            {timeline.map((row) => <TimelineItem key={row.id} row={row} timeZone={timeZone} />)}
          </ol>
          <div className="text-secondary small">Times are shown in {timeZone}. Struck-through rows were reversed by an admin.</div>
        </>
      )}
    </>
  )
}
