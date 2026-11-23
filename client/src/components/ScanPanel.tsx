import { useState, type KeyboardEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { configApi } from '../api/endpoints'
import { useUnitLookup } from '../hooks/useUnitLookup'
import { isValidBarcode, normalizeBarcode } from '../lib/barcode'
import { formatDateTime } from '../lib/dates'
import { ErrorAlert } from './Alerts'
import { StatusBadge } from './StatusBadge'

interface Props {
  /** The barcode that was scanned (Enter pressed and valid); '' while nothing is scanned. Owned by the page. */
  committed: string
  /** Called with the normalized barcode on Enter, or with '' when the user edits the text after a scan. */
  onCommit: (barcode: string) => void
  label?: string
  /** Pre-filled barcode, e.g. from ?barcode= after a reversal. Only used when the panel first appears. */
  initialBarcode?: string
}

/**
 * Barcode input + unit card, shared by Track, Receive and Deliver.
 * A USB scanner types the barcode and then presses Enter, exactly like a person would; so Enter is the trigger.
 * Pages "scan next" by changing the React `key` of this component: that remounts it, which empties the box and
 * (because of autoFocus) puts the cursor back in it.
 */
export function ScanPanel({ committed, onCommit, label = 'Unit barcode', initialBarcode = '' }: Props) {
  const [text, setText] = useState(initialBarcode)
  const [formatError, setFormatError] = useState<string | null>(null)
  const lookup = useUnitLookup(committed)
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key !== 'Enter') return
    e.preventDefault() // do not submit the surrounding form: Enter here means "look this barcode up"
    const code = normalizeBarcode(text)
    if (!isValidBarcode(code)) {
      setFormatError('Barcode must be 6-32 characters: A-Z, 0-9 and "-".')
      return
    }
    setFormatError(null)
    setText(code)
    onCommit(code)
  }

  const unit = lookup.data
  const timeZone = config.data?.timeZone ?? 'UTC'

  return (
    <div className="mb-3">
      <label htmlFor="scan-barcode" className="form-label fw-semibold">{label}</label>
      <input
        id="scan-barcode" className={`form-control form-control-lg ${formatError ? 'is-invalid' : ''}`}
        autoFocus autoComplete="off" spellCheck={false} placeholder="Scan or type, then press Enter"
        value={text}
        onChange={(e) => {
          const next = e.target.value.toUpperCase() // uppercase as you type (A13)
          setText(next)
          setFormatError(null)
          if (committed && normalizeBarcode(next) !== committed) onCommit('') // edited after a scan: forget the old unit
        }}
        onKeyDown={onKeyDown}
      />
      <div className="invalid-feedback">{formatError}</div>

      {committed && <ErrorAlert error={lookup.error} />}
      {unit && (
        <div className="card mt-3" data-testid="unit-card">
          <div className="card-body py-2">
            <div className="d-flex flex-wrap align-items-center gap-2">
              <code className="fs-6">{unit.barcode}</code>
              <StatusBadge status={unit.status} />
              {unit.exists && <span>{unit.product}</span>}
              {unit.currentPoint && unit.since && (
                <span className="text-secondary small">
                  {unit.status === 'Delivered' ? 'delivered from' : 'at'} {unit.currentPoint}, {formatDateTime(unit.since, timeZone)}
                </span>
              )}
            </div>
            {!unit.exists && <div className="small text-secondary mt-1">Not registered yet. The first scan registers it.</div>}
            {unit.components.length > 0 && (
              <div className="small mt-2">
                <span className="text-secondary">Components:</span>{' '}
                {unit.components.map((c) => `${c.componentName} (${c.componentBarcode})`).join(', ')}
              </div>
            )}
            {unit.recentEvents.length > 0 && (
              <details className="small mt-1">
                <summary className="text-secondary">Recent events</summary>
                <ul className="mb-0">
                  {unit.recentEvents.map((ev) => (
                    <li key={ev.id}>
                      {formatDateTime(ev.occurredAt, timeZone)} {ev.eventType} at {ev.pointName}
                      {ev.componentBarcode ? ` (${ev.componentName} ${ev.componentBarcode})` : ''} by {ev.performedBy}
                    </li>
                  ))}
                </ul>
              </details>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
