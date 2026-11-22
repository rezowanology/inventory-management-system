import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { MasterDataInput, MasterDataRow } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { Modal } from '../../components/Modal'
import { applyServerErrors } from '../../lib/serverErrors'
import { POINT_TYPES } from './constants'

// The same rules as the server (MasterDataRequest): 2-100 characters after trimming. The server stays the authority.
const schema = z.object({
  name: z.string().trim().min(2, 'The name must have at least 2 characters').max(100, 'The name can have at most 100 characters'),
  type: z.enum(POINT_TYPES, { message: 'Choose a type' }).optional(),
})
type FormValues = z.infer<typeof schema>

interface Props {
  /** "Product", "Component" or "Point": used in the title. */
  singular: string
  /** Points have a type, the other two do not. */
  hasType: boolean
  /** The row being edited; undefined = create a new one. */
  editing?: MasterDataRow
  onSubmit: (values: MasterDataInput) => Promise<unknown>
  onClose: () => void
}

/** Create / edit dialog for products, components and points (react-hook-form + zod). */
export function MasterDataForm({ singular, hasType, editing, onSubmit, onClose }: Props) {
  const [generalError, setGeneralError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: editing?.name ?? '', type: editing?.pointType ?? undefined },
  })

  async function submit(values: FormValues) {
    setGeneralError(null)
    if (hasType && !values.type) { setError('type', { message: 'Choose a type' }); return }
    try {
      await onSubmit({ name: values.name, type: hasType ? values.type : undefined })
    } catch (e) {
      // Field errors from the server (e.g. "name already exists") go under the input; anything else into the alert.
      setGeneralError(applyServerErrors(e, setError, ['name', 'type']))
    }
  }

  return (
    <Modal title={`${editing ? 'Edit' : 'New'} ${singular.toLowerCase()}`} onClose={onClose}>
      <form onSubmit={handleSubmit(submit)} noValidate>
        <div className="modal-body">
          {generalError && <ErrorAlert error={new Error(generalError)} />}
          <div className="mb-3">
            <label htmlFor="md-name" className="form-label">Name</label>
            <input id="md-name" className={`form-control ${errors.name ? 'is-invalid' : ''}`} autoFocus {...register('name')} />
            <div className="invalid-feedback">{errors.name?.message}</div>
          </div>
          {hasType && (
            <div className="mb-3">
              <label htmlFor="md-type" className="form-label">Type</label>
              <select id="md-type" className={`form-select ${errors.type ? 'is-invalid' : ''}`} {...register('type')}>
                <option value="">Choose...</option>
                {POINT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
              <div className="invalid-feedback">{errors.type?.message}</div>
            </div>
          )}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn btn-outline-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>Save</button>
        </div>
      </form>
    </Modal>
  )
}
