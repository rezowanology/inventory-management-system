import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router'
import { configApi, historyApi } from '../../api/endpoints'
import type { ReverseResult } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { formatDateTime } from '../../lib/dates'
import { ReverseForm } from './ReverseForm'

// After a reversal the admin usually wants to enter the CORRECT data (A4): link to the matching scan screen.
const OPS_PAGE: Record<string, { to: string; label: string }> = {
  COMPONENT_TRACKED: { to: '/ops/track', label: 'Track components' },
  RECEIVED: { to: '/ops/receive', label: 'Receive' },
  DELIVERED: { to: '/ops/deliver', label: 'Deliver' },
}

/** /history/:id/reverse: the original row, what the reversal would change, a required reason, and the result. */
export function ReversePage() {
  const id = Number(useParams().id)
  const queryClient = useQueryClient()
  const preview = useQuery({ queryKey: ['history-reverse-preview', id], queryFn: () => historyApi.reversePreview(id), enabled: Number.isFinite(id) })
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  const timeZone = config.data?.timeZone ?? 'UTC'

  const reverse = useMutation({
    mutationFn: (reason: string) => historyApi.reverse(id, reason),
    onSuccess: () => {
      // The reversal changes history, the unit's status, stock, the dashboard and every report: mark them all out of date.
      for (const key of ['history', 'history-reverse-preview', 'unit', 'dashboard', 'reports']) {
        void queryClient.invalidateQueries({ queryKey: [key] })
      }
    },
  })

  if (preview.isPending) return <p className="text-secondary">Loading...</p>
  if (preview.isError) {
    return (
      <>
        <ErrorAlert error={preview.error} />
        <Link to="/history">Back to history</Link>
      </>
    )
  }

  const { row, allowed, blockedReason, effect } = preview.data
  const done: ReverseResult | undefined = reverse.data
  const next = OPS_PAGE[row.eventType]

  return (
    <>
      <h1 className="h4 mb-3">Reverse a history row</h1>

      <div className="card mb-3">
        <div className="card-header">Original row #{row.id}</div>
        <div className="card-body">
          <dl className="row mb-0">
            <dt className="col-sm-3">Time</dt><dd className="col-sm-9">{formatDateTime(row.occurredAt, timeZone)}</dd>
            <dt className="col-sm-3">Unit</dt><dd className="col-sm-9"><code>{row.unitBarcode}</code> ({row.productName})</dd>
            <dt className="col-sm-3">Event</dt><dd className="col-sm-9">{row.eventType} at {row.pointName}</dd>
            {row.componentName && (<><dt className="col-sm-3">Component</dt><dd className="col-sm-9">{row.componentName} (<code>{row.componentBarcode}</code>)</dd></>)}
            <dt className="col-sm-3">Done by</dt><dd className="col-sm-9 mb-0">{row.performedByEmployeeNo} {row.performedByName}</dd>
          </dl>
        </div>
      </div>

      {done ? (
        <div className="alert alert-success" role="status">
          <strong>Reversed.</strong> {done.effect}
          <div className="mt-2 d-flex flex-wrap gap-2">
            {next && (
              <Link className="btn btn-primary btn-sm" to={`${next.to}?barcode=${encodeURIComponent(done.unitBarcode)}`}>
                Re-enter the correct entry ({next.label})
              </Link>
            )}
            <Link className="btn btn-outline-secondary btn-sm" to={`/reports/barcode?barcode=${encodeURIComponent(done.unitBarcode)}`}>See its full history</Link>
            <Link className="btn btn-outline-secondary btn-sm" to="/history">Back to history</Link>
          </div>
        </div>
      ) : allowed ? (
        <>
          <div className="alert alert-info" role="status"><strong>What will change:</strong> {effect}</div>
          <p className="small text-secondary">
            The original row is never edited or deleted: a new REVERSED row is added with your name and the reason below.
          </p>
          <ReverseForm onSubmit={(reason) => reverse.mutateAsync(reason)} />
        </>
      ) : (
        <div className="alert alert-warning" role="alert"><strong>This row cannot be reversed.</strong> {blockedReason}</div>
      )}
    </>
  )
}
