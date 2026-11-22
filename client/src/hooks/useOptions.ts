import { useQuery } from '@tanstack/react-query'
import { masterDataApi } from '../api/endpoints'
import type { MasterKind, PointType } from '../api/types'

/** Dropdown contents (active rows only) for products, components or points of one type. Cached by kind + type. */
export function useOptions(kind: MasterKind, type?: PointType) {
  return useQuery({ queryKey: ['options', kind, type ?? null], queryFn: () => masterDataApi.options(kind, type) })
}
