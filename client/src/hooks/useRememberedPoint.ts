import { useState } from 'react'

/**
 * Remembers the last point a user picked for the rest of the browser session, so the next scan does not need
 * the dropdown again. sessionStorage can throw (private mode, storage blocked), so every access is wrapped.
 */
export function useRememberedPoint(storageKey: string): [string, (value: string) => void] {
  const [value, setValue] = useState(() => {
    try { return sessionStorage.getItem(storageKey) ?? '' } catch { return '' }
  })
  function remember(next: string) {
    setValue(next)
    try { sessionStorage.setItem(storageKey, next) } catch { /* ignore: the choice just is not remembered */ }
  }
  return [value, remember]
}
