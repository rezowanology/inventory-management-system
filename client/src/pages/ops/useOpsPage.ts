import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { configApi } from '../../api/endpoints'
import { useUnitLookup } from '../../hooks/useUnitLookup'

/**
 * State every scan page shares: which barcode is scanned, the "scan next" counter, the success message and the
 * unit lookup. `finish` runs after a successful save: it shows the message, clears the barcode and bumps `scanId`
 * (the page uses it as the `key` of ScanPanel, which remounts the panel and puts the cursor back in the box).
 */
export function useOpsPage() {
  const queryClient = useQueryClient()
  const [barcode, setBarcode] = useState('')
  const [scanId, setScanId] = useState(0)
  const [notice, setNotice] = useState<string | null>(null)
  const unit = useUnitLookup(barcode).data
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })

  function finish(message: string) {
    setNotice(message)
    setBarcode('')
    setScanId((n) => n + 1)
    // The saved rows change the unit card, the history list and the dashboard counts: mark them out of date.
    void queryClient.invalidateQueries({ queryKey: ['unit'] })
    void queryClient.invalidateQueries({ queryKey: ['history'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }

  return { barcode, setBarcode, scanId, notice, setNotice, unit, timeZone: config.data?.timeZone ?? 'UTC', finish }
}
