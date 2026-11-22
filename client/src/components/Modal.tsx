import type { ReactNode } from 'react'

/**
 * A Bootstrap-styled dialog controlled by React (rendered only while it is open), so we do not need
 * Bootstrap's own JavaScript for it. Clicking the dark backdrop or the X closes it.
 */
export function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  return (
    <>
      <div className="modal d-block" tabIndex={-1} role="dialog" aria-modal="true" aria-label={title} onClick={onClose}>
        {/* stopPropagation: a click inside the dialog must not reach the backdrop handler above */}
        <div className="modal-dialog" onClick={(e) => e.stopPropagation()}>
          <div className="modal-content">
            <div className="modal-header">
              <h2 className="modal-title h5">{title}</h2>
              <button type="button" className="btn-close" aria-label="Close" onClick={onClose} />
            </div>
            {children}
          </div>
        </div>
      </div>
      <div className="modal-backdrop show" />
    </>
  )
}
