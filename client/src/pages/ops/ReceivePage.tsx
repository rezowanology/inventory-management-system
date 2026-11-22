import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { opsApi } from '../../api/endpoints'
import { ScanErrorAlert } from '../../components/Alerts'
import { OptionSelect } from '../../components/OptionSelect'
import { ScanPanel } from '../../components/ScanPanel'
import { useOptions } from '../../hooks/useOptions'
import { useRememberedPoint } from '../../hooks/useRememberedPoint'
import { useOpsPage } from './useOpsPage'

/** /ops/receive: scan a unit, choose the receive point, save. A new barcode also needs its product. */
export function ReceivePage() {
  const { barcode, setBarcode, scanId, notice, setNotice, unit, timeZone, finish } = useOpsPage()
  const [rememberedPoint, rememberPoint] = useRememberedPoint('ims.receivePoint')
  const [productId, setProductId] = useState('')
  const products = useOptions('products')
  const points = useOptions('points', 'RECEIVE')

  // A remembered point that was archived meanwhile is no longer in the list: treat it as "not chosen".
  const pointId = points.data?.some((p) => String(p.id) === rememberedPoint) ? rememberedPoint : ''
  const isNew = unit?.exists === false

  const save = useMutation({
    mutationFn: () => opsApi.receive({
      barcode, productId: isNew ? Number(productId) : undefined, receivePointId: Number(pointId),
    }),
    onSuccess: (updated) => { finish(`Unit ${updated.barcode} received at ${updated.currentPoint}. Scan the next one.`); setProductId('') },
  })

  const canSave = !!unit && !!pointId && (!isNew || !!productId) && !save.isPending

  return (
    <>
      <h1 className="h4 mb-3">Receive units</h1>
      {notice && <div className="alert alert-success" role="status">{notice}</div>}
      <ScanPanel key={scanId} committed={barcode} onCommit={(b) => { setBarcode(b); setNotice(null); save.reset() }} />
      <ScanErrorAlert error={save.error} timeZone={timeZone} />

      {unit && (
        <form onSubmit={(e) => { e.preventDefault(); if (canSave) save.mutate() }} noValidate>
          {isNew && <OptionSelect id="product" label="Product (new unit)" options={products.data} value={productId} onChange={setProductId} />}
          <OptionSelect id="point" label="Receive point" options={points.data} value={pointId} onChange={rememberPoint} />
          <button type="submit" className="btn btn-primary" disabled={!canSave}>Receive</button>
        </form>
      )}
    </>
  )
}
