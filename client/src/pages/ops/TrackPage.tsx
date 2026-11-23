import { useRef, useState } from 'react'
import { useFieldArray, useForm, type FieldPath } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { z } from 'zod'
import { opsApi } from '../../api/endpoints'
import { ScanErrorAlert } from '../../components/Alerts'
import { OptionSelect } from '../../components/OptionSelect'
import { ScanPanel } from '../../components/ScanPanel'
import { useOptions } from '../../hooks/useOptions'
import { useRememberedPoint } from '../../hooks/useRememberedPoint'
import { checkComponentScan } from '../../lib/batch'
import { isValidBarcode, normalizeBarcode } from '../../lib/barcode'
import { applyServerErrors } from '../../lib/serverErrors'
import { useOpsPage } from './useOpsPage'

// zod mirrors the server rules for fast feedback; the server stays the authority.
const schema = z.object({
  productId: z.string(),
  trackPointId: z.string(), // the point comes from the dropdown + sessionStorage (see pointId); checked in submit()
  components: z.array(z.object({
    componentId: z.string().min(1),
    componentBarcode: z.string().refine(isValidBarcode, 'Barcode must be 6-32 characters: A-Z, 0-9 and "-".'),
  })).min(1, 'Add at least one component'),
})
type FormValues = z.infer<typeof schema>

/** /ops/track: scan the unit, pick the track point, scan components into a batch, save all at once. */
export function TrackPage() {
  const { initialBarcode, barcode, setBarcode, scanId, notice, setNotice, unit, timeZone, finish } = useOpsPage()
  const [rememberedPoint, rememberPoint] = useRememberedPoint('ims.trackPoint')
  const products = useOptions('products')
  const components = useOptions('components')
  const points = useOptions('points', 'TRACK')
  const pointId = points.data?.some((p) => String(p.id) === rememberedPoint) ? rememberedPoint : ''

  const { register, control, handleSubmit, setError, clearErrors, reset, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { productId: '', trackPointId: '', components: [] },
  })
  // useFieldArray manages the list of component rows (append / remove) inside the form state.
  const { fields, append, remove } = useFieldArray({ control, name: 'components' })

  // The "add a component" mini row. Enter in the barcode box adds the row (what a scanner does).
  const [newComponentId, setNewComponentId] = useState('')
  const [newCode, setNewCode] = useState('')
  const [rowWarning, setRowWarning] = useState<string | null>(null)
  const codeInput = useRef<HTMLInputElement>(null)

  const save = useMutation({
    mutationFn: (v: FormValues) => opsApi.track({
      barcode,
      productId: unit?.exists === false ? Number(v.productId) : undefined,
      trackPointId: Number(pointId),
      components: v.components.map((c) => ({ componentId: Number(c.componentId), componentBarcode: c.componentBarcode })),
    }),
    onSuccess: (updated, v) => {
      finish(`Unit ${updated.barcode}: ${v.components.length} component(s) tracked. Scan the next unit.`)
      reset({ productId: '', trackPointId: '', components: [] })
      setNewCode('')
    },
  })

  function addComponent() {
    const code = normalizeBarcode(newCode)
    if (!newComponentId) { setRowWarning('Choose the component first.'); return }
    if (!isValidBarcode(code)) { setRowWarning('Barcode must be 6-32 characters: A-Z, 0-9 and "-".'); return }
    const check = checkComponentScan(code, barcode, fields.map((f) => f.componentBarcode))
    if (!check.ok) { setRowWarning(check.reason); setNewCode(''); codeInput.current?.focus(); return } // warn, do not add
    setRowWarning(null)
    append({ componentId: newComponentId, componentBarcode: code })
    setNewCode('')
    codeInput.current?.focus() // ready for the next component
  }

  async function submit(values: FormValues) {
    if (!pointId) { setError('trackPointId', { message: 'Choose the track point' }); return }
    if (unit?.exists === false && !values.productId) { setError('productId', { message: 'Choose the product of this new unit' }); return }
    try {
      await save.mutateAsync(values)
    } catch (e) {
      // Put field errors (also "components[2].componentBarcode") on the matching row; the alert above shows the message.
      const known: FieldPath<FormValues>[] = ['productId', 'trackPointId', 'components']
      fields.forEach((_, i) => known.push(`components.${i}.componentBarcode`, `components.${i}.componentId`))
      applyServerErrors(e, setError, known)
    }
  }

  const componentName = (id: string) => components.data?.find((c) => String(c.id) === id)?.name ?? id

  return (
    <>
      <h1 className="h4 mb-3">Track components</h1>
      {notice && <div className="alert alert-success" role="status">{notice}</div>}
      <ScanPanel
        key={scanId} label="Product barcode (the unit)" initialBarcode={initialBarcode} committed={barcode}
        onCommit={(b) => { setBarcode(b); setNotice(null); save.reset(); setRowWarning(null) }}
      />

      {unit && (
        <form onSubmit={handleSubmit(submit)} noValidate>
          {unit.exists === false && (
            <div className="mb-3">
              <label htmlFor="product" className="form-label">Product (new unit)</label>
              <select id="product" className={`form-select ${errors.productId ? 'is-invalid' : ''}`} {...register('productId')}>
                <option value="">Choose...</option>
                {products.data?.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
              <div className="invalid-feedback">{errors.productId?.message}</div>
            </div>
          )}

          <OptionSelect id="point" label="Track point" options={points.data} value={pointId}
            error={errors.trackPointId?.message}
            onChange={(v) => { rememberPoint(v); clearErrors('trackPointId') }} />

          <fieldset className="border rounded p-3 mb-3">
            <legend className="fs-6 float-none w-auto px-2">Components</legend>
            <div className="row g-2 align-items-end">
              <div className="col-md-4">
                <label htmlFor="new-component" className="form-label">Component</label>
                <select id="new-component" className="form-select" value={newComponentId} onChange={(e) => setNewComponentId(e.target.value)}>
                  <option value="">Choose...</option>
                  {components.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <div className="col-md-5">
                <label htmlFor="new-code" className="form-label">Component barcode</label>
                <input id="new-code" ref={codeInput} className="form-control" autoComplete="off" spellCheck={false}
                  placeholder="Scan, then Enter" value={newCode}
                  onChange={(e) => { setNewCode(e.target.value.toUpperCase()); setRowWarning(null) }}
                  onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addComponent() } }} />
              </div>
              <div className="col-md-3">
                <button type="button" className="btn btn-outline-primary w-100" onClick={addComponent}>Add</button>
              </div>
            </div>
            {rowWarning && <div className="alert alert-warning py-2 mt-2 mb-0" role="alert">{rowWarning}</div>}

            {fields.length > 0 && (
              <table className="table table-sm align-middle mt-3 mb-0">
                <thead><tr><th>Component</th><th>Barcode</th><th /></tr></thead>
                <tbody>
                  {fields.map((f, i) => {
                    const rowError = errors.components?.[i]?.componentBarcode?.message ?? errors.components?.[i]?.componentId?.message
                    return (
                      <tr key={f.id} className={rowError ? 'table-danger' : undefined}>
                        <td>{componentName(f.componentId)}</td>
                        <td><code>{f.componentBarcode}</code>{rowError && <div className="text-danger small">{rowError}</div>}</td>
                        <td className="text-end">
                          <button type="button" className="btn btn-sm btn-outline-danger" aria-label={`Remove ${f.componentBarcode}`}
                            onClick={() => remove(i)}>Remove</button>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            )}
            {typeof errors.components?.message === 'string' && <div className="text-danger small mt-2">{errors.components.message}</div>}
          </fieldset>

          <ScanErrorAlert error={save.error} timeZone={timeZone} />
          <button type="submit" className="btn btn-primary" disabled={save.isPending}>Save components</button>
        </form>
      )}
    </>
  )
}
