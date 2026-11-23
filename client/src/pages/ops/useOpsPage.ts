import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router'
import { configApi } from '../../api/endpoints'
import { useUnitLookup } from '../../hooks/useUnitLookup'
import { isValidBarcode, normalizeBarcode } from '../../lib/barcode'

/**
 * State every scan page shares: which barcode is scanned, the "scan next" counter, the success message and the
 * unit lookup. `finish` runs after a successful save: it shows the message, clears the barcode and bumps `scanId`
 * (the page uses it as the `key` of ScanPanel, which remounts the panel and puts the cursor back in the box).
 */
export function useOpsPage() {
  const queryClient = useQueryClient()
  // /ops/receive?barcode=ABC123 (the link shown after a reversal) pre-fills and looks up that barcode; a bad value is ignored.
  const [search] = useSearchParams()
  const [fromUrl] = useState(() => {
    const b = normalizeBarcode(search.get('barcode'))
    return isValidBarcode(b) ? b : ''
  })
  const [barcode, setBarcode] = useState(fromUrl)
  const [scanId, setScanId] = useState(0)
  const [notice, setNotice] = useState<string | null>(null)
  const unit = useUnitLookup(barcode).data
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })

  function finish(message: string) {
    setNotice(message)
    setBarcode('')
    setScanId((n) => n + 1)
    // The saved rows change the unit card, the history list, the reports and the dashboard counts: mark them out of date.
    void queryClient.invalidateQueries({ queryKey: ['unit'] })
    void queryClient.invalidateQueries({ queryKey: ['history'] })
    void queryClient.invalidateQueries({ queryKey: ['reports'] })
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }

  // Only the FIRST panel starts filled; after "scan next" (scanId > 0) the box must be empty again.
  const initialBarcode = scanId === 0 ? fromUrl : ''

  return { initialBarcode, barcode, setBarcode, scanId, notice, setNotice, unit, timeZone: config.data?.timeZone ?? 'UTC', finish }
}
