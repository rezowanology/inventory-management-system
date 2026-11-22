import { describe, expect, it } from 'vitest'
import { BARCODE_PATTERN, isValidBarcode, normalizeBarcode } from './barcode'

describe('barcode', () => {
  // Pinned on purpose: the server has the identical test (Barcode.Pattern). Change both together.
  it('uses the agreed pattern', () => {
    expect(BARCODE_PATTERN).toBe('^[A-Z0-9-]{6,32}$')
  })

  it('trims and upper-cases', () => {
    expect(normalizeBarcode('  2440701ak2057 ')).toBe('2440701AK2057')
    expect(normalizeBarcode(null)).toBe('')
  })

  it('accepts the sample barcodes', () => {
    expect(isValidBarcode('2440701AK2057')).toBe(true)
    expect(isValidBarcode('94A600321852')).toBe(true)
  })

  it('rejects wrong shapes', () => {
    expect(isValidBarcode('ABC12')).toBe(false) // too short
    expect(isValidBarcode('A'.repeat(33))).toBe(false) // too long
    expect(isValidBarcode('ABC 123')).toBe(false) // space
    expect(isValidBarcode('abc123')).toBe(false) // not normalized yet
  })
})
