import type { Option } from '../api/types'

interface Props {
  id: string
  label: string
  options: Option[] | undefined
  value: string
  onChange: (value: string) => void
  error?: string
  disabled?: boolean
}

/** A labelled dropdown fed by /api/<kind>/options (products, components or points of one type). */
export function OptionSelect({ id, label, options, value, onChange, error, disabled }: Props) {
  return (
    <div className="mb-3">
      <label htmlFor={id} className="form-label">{label}</label>
      <select id={id} className={`form-select ${error ? 'is-invalid' : ''}`} value={value} disabled={disabled}
        onChange={(e) => onChange(e.target.value)}>
        <option value="">{options ? 'Choose...' : 'Loading...'}</option>
        {options?.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
      </select>
      <div className="invalid-feedback">{error}</div>
    </div>
  )
}
