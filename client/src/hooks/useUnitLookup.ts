import { useQuery } from '@tanstack/react-query'
import { unitsApi } from '../api/endpoints'
import { isValidBarcode } from '../lib/barcode'

/**
 * Looks a barcode up (GET /api/units/lookup). The query key is ['unit', barcode]; it stays idle until the
 * barcode has a valid shape. The scan pages and the ScanPanel call this hook with the same barcode, so React Query
 * shares ONE request/cache entry between them. After a save, invalidate ['unit'] to refresh the unit card.
 */
export function useUnitLookup(barcode: string) {
  return useQuery({
    queryKey: ['unit', barcode],
    queryFn: () => unitsApi.lookup(barcode),
    enabled: isValidBarcode(barcode),
  })
}
