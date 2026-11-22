import { expect, it } from 'vitest'
import { formatDateTime } from './dates'

it('shows UTC times in the configured zone (sample record reads 10:00:00 in Dhaka)', () => {
  expect(formatDateTime('2026-03-27T04:00:00Z', 'Asia/Dhaka')).toBe('2026-03-27 10:00:00')
  expect(formatDateTime('2026-03-30T05:30:00Z', 'Asia/Dhaka')).toBe('2026-03-30 11:30:00')
})
