import type { UnitStatusName } from '../api/types'

// Registered grey, InStock green, Delivered blue, NotFound amber.
const STYLE: Record<UnitStatusName, { cls: string; label: string }> = {
  Registered: { cls: 'text-bg-secondary', label: 'Registered' },
  InStock: { cls: 'text-bg-success', label: 'In stock' },
  Delivered: { cls: 'text-bg-primary', label: 'Delivered' },
  NotFound: { cls: 'text-bg-warning', label: 'New barcode' },
}

export function StatusBadge({ status }: { status: UnitStatusName }) {
  const { cls, label } = STYLE[status]
  return <span className={`badge ${cls}`}>{label}</span>
}
