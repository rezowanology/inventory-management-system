import { useMutation } from '@tanstack/react-query'
import { opsApi } from '../../api/endpoints'
import { ScanErrorAlert } from '../../components/Alerts'
import { OptionSelect } from '../../components/OptionSelect'
import { ScanPanel } from '../../components/ScanPanel'
import { useOptions } from '../../hooks/useOptions'
import { useRememberedPoint } from '../../hooks/useRememberedPoint'
import { useOpsPage } from './useOpsPage'

/** /ops/deliver: scan a unit, choose the delivery point, save. Only an in-stock unit can be delivered. */
export function DeliverPage() {
  const { initialBarcode, barcode, setBarcode, scanId, notice, setNotice, unit, timeZone, finish } = useOpsPage()
  const [rememberedPoint, rememberPoint] = useRememberedPoint('ims.deliveryPoint')
  const points = useOptions('points', 'DELIVERY')
  const pointId = points.data?.some((p) => String(p.id) === rememberedPoint) ? rememberedPoint : ''

  const save = useMutation({
    mutationFn: () => opsApi.deliver({ barcode, deliveryPointId: Number(pointId) }),
    onSuccess: (updated) => finish(`Unit ${updated.barcode} delivered. Scan the next one.`),
  })

  // The button is only a convenience: the server checks "in stock" again when saving.
  const inStock = unit?.status === 'InStock'
  const canSave = inStock && !!pointId && !save.isPending

  return (
    <>
      <h1 className="h4 mb-3">Deliver units</h1>
      {notice && <div className="alert alert-success" role="status">{notice}</div>}
      <ScanPanel key={scanId} initialBarcode={initialBarcode} committed={barcode} onCommit={(b) => { setBarcode(b); setNotice(null); save.reset() }} />
      <ScanErrorAlert error={save.error} timeZone={timeZone} />

      {unit && !inStock && (
        <div className="alert alert-info">
          {unit.exists ? 'Only units that are in stock can be delivered.' : 'This barcode is not registered, so it cannot be delivered.'}
        </div>
      )}
      {unit && (
        <form onSubmit={(e) => { e.preventDefault(); if (canSave) save.mutate() }} noValidate>
          <OptionSelect id="point" label="Delivery point" options={points.data} value={pointId} onChange={rememberPoint} disabled={!inStock} />
          <button type="submit" className="btn btn-primary" disabled={!canSave}>Deliver</button>
        </form>
      )}
    </>
  )
}
