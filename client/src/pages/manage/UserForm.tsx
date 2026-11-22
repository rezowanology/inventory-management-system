import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { CreateUserInput, UpdateUserInput, UserRow } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { Modal } from '../../components/Modal'
import { applyServerErrors } from '../../lib/serverErrors'
import { ROLES } from './constants'

const schema = z.object({
  userName: z.string().trim().min(3, 'At least 3 characters').max(150),
  firstName: z.string().trim().min(1, 'First name is required').max(150),
  lastName: z.string().trim().min(1, 'Last name is required').max(150),
  employeeNo: z.string().trim().min(2, 'At least 2 characters').max(20, 'At most 20 characters'),
  role: z.enum(ROLES, { message: 'Choose a role' }),
  password: z.string(),
})
type FormValues = z.infer<typeof schema>
const FIELDS = ['userName', 'firstName', 'lastName', 'employeeNo', 'role', 'password'] as const

interface Props {
  /** undefined = create a new user. */
  editing?: UserRow
  onCreate: (input: CreateUserInput) => Promise<unknown>
  onUpdate: (id: number, input: UpdateUserInput) => Promise<unknown>
  onClose: () => void
}

/** Create / edit dialog for users. A user has exactly one role (a single select). */
export function UserForm({ editing, onCreate, onUpdate, onClose }: Props) {
  const [generalError, setGeneralError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      userName: editing?.userName ?? '', firstName: editing?.firstName ?? '', lastName: editing?.lastName ?? '',
      employeeNo: editing?.employeeNo ?? '', role: editing?.role, password: '',
    },
  })

  async function submit(v: FormValues) {
    setGeneralError(null)
    if (!editing && v.password.length === 0) { setError('password', { message: 'An initial password is required' }); return }
    try {
      if (editing) await onUpdate(editing.id, { firstName: v.firstName, lastName: v.lastName, employeeNo: v.employeeNo, role: v.role })
      else await onCreate({ ...v })
    } catch (e) {
      setGeneralError(applyServerErrors(e, setError, FIELDS))
    }
  }

  const input = (name: keyof FormValues, label: string, opts: { type?: string; readOnly?: boolean; hint?: string } = {}) => (
    <div className="mb-3">
      <label htmlFor={`u-${name}`} className="form-label">{label}</label>
      <input id={`u-${name}`} type={opts.type ?? 'text'} readOnly={opts.readOnly}
        className={`form-control ${errors[name] ? 'is-invalid' : ''}`}
        autoComplete={name === 'password' ? 'new-password' : 'off'} {...register(name)} />
      {opts.hint && <div className="form-text">{opts.hint}</div>}
      <div className="invalid-feedback">{errors[name]?.message}</div>
    </div>
  )

  return (
    <Modal title={editing ? `Edit ${editing.userName}` : 'New user'} onClose={onClose}>
      <form onSubmit={handleSubmit(submit)} noValidate>
        <div className="modal-body">
          {generalError && <ErrorAlert error={new Error(generalError)} />}
          {input('userName', 'User name (login)', { readOnly: !!editing, hint: editing ? 'The login name cannot be changed.' : undefined })}
          {input('firstName', 'First name')}
          {input('lastName', 'Last name')}
          {input('employeeNo', 'Employee number')}
          <div className="mb-3">
            <label htmlFor="u-role" className="form-label">Role</label>
            <select id="u-role" className={`form-select ${errors.role ? 'is-invalid' : ''}`} {...register('role')}>
              <option value="">Choose...</option>
              {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
            </select>
            <div className="invalid-feedback">{errors.role?.message}</div>
          </div>
          {!editing && input('password', 'Initial password', {
            type: 'password', hint: 'At least 8 characters with an upper-case letter, a lower-case letter, a digit and a symbol.',
          })}
        </div>
        <div className="modal-footer">
          <button type="button" className="btn btn-outline-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>Save</button>
        </div>
      </form>
    </Modal>
  )
}
