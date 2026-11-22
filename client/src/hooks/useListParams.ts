import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'

/** What a list page reads from the URL. */
export interface ListParams {
  q: string
  page: number
  /** Filter values by key. A filter that is not in the URL has its default value (or an empty string). */
  filters: Record<string, string>
}

/**
 * Pure helper (easy to unit-test): turns URL search params into list parameters.
 * Bad input is repaired instead of crashing: "?page=abc" or "?page=-4" becomes page 1.
 */
export function parseListParams(
  search: URLSearchParams,
  filterKeys: readonly string[] = [],
  defaults: Record<string, string> = {},
): ListParams {
  const page = Math.max(1, Math.floor(Number(search.get('page'))) || 1)
  const filters: Record<string, string> = {}
  for (const key of filterKeys) filters[key] = search.get(key) ?? defaults[key] ?? ''
  return { q: search.get('q') ?? '', page, filters }
}

/** Builds the query string for the API call: empty values are left out, so the URL stays short. */
export function toQueryString(values: Record<string, string | number | undefined | null>): string {
  const out = new URLSearchParams()
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== null && String(value) !== '') out.set(key, String(value))
  }
  return out.toString()
}

/**
 * Keeps the list state (search text, page, filters) in the URL, e.g. /manage/products?q=fri&status=all&page=2.
 * Why the URL and not useState: the Back button, a browser refresh and a copied link all keep working.
 * Changing the search text or a filter jumps back to page 1.
 */
export function useListParams(filterKeys: readonly string[] = [], defaults: Record<string, string> = {}) {
  const [search, setSearch] = useSearchParams()
  // Callers pass new array/object literals on every render, so we compare their contents instead.
  const keysId = filterKeys.join('|')
  const defaultsId = JSON.stringify(defaults)

  const params = useMemo(
    () => parseListParams(search, filterKeys, defaults),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [search, keysId, defaultsId],
  )

  const update = useCallback((changes: Record<string, string | null>, keepPage = false) => {
    setSearch((current) => {
      const next = new URLSearchParams(current)
      for (const [key, value] of Object.entries(changes)) {
        // An empty value or the default value is removed from the URL.
        if (value === null || value === '' || value === defaults[key]) next.delete(key)
        else next.set(key, value)
      }
      if (!keepPage) next.delete('page')
      return next
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [setSearch, defaultsId])

  return {
    params,
    setQ: (q: string) => update({ q: q.trim() }),
    setFilter: (key: string, value: string) => update({ [key]: value }),
    setPage: (page: number) => update({ page: page <= 1 ? null : String(page) }, true),
  }
}
