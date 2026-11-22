import type { ReactNode } from 'react'
import { Modal } from './Modal'

interface Props {
  title: string
  children: ReactNode
  confirmLabel: string
  /** Bootstrap colour of the confirm button: "danger" for destructive actions. */
  variant?: 'danger' | 'primary'
  busy?: boolean
  onConfirm: () => void
  onCancel: () => void
}

/** "Are you sure?" dialog used before every delete / deactivate. */
export function ConfirmDialog({ title, children, confirmLabel, variant = 'danger', busy, onConfirm, onCancel }: Props) {
  return (
    <Modal title={title} onClose={onCancel}>
      <div className="modal-body">{children}</div>
      <div className="modal-footer">
        <button type="button" className="btn btn-outline-secondary" onClick={onCancel} disabled={busy}>Cancel</button>
        <button type="button" className={`btn btn-${variant}`} onClick={onConfirm} disabled={busy}>{confirmLabel}</button>
      </div>
    </Modal>
  )
}
