import { describe, expect, it } from 'vitest'
import { checkComponentScan } from './batch'

describe('checkComponentScan', () => {
  it('accepts a new component barcode', () => {
    expect(checkComponentScan('94A600321852', 'TESTUNIT0001', ['AAAAAA111111'])).toEqual({ ok: true })
  })

  it('refuses a component barcode already in the batch', () => {
    const result = checkComponentScan('94A600321852', 'TESTUNIT0001', ['94A600321852'])
    expect(result.ok).toBe(false)
    expect(!result.ok && result.reason).toContain('Duplicate scan')
  })

  it('refuses the product barcode scanned as a component', () => {
    const result = checkComponentScan('TESTUNIT0001', 'TESTUNIT0001', [])
    expect(result.ok).toBe(false)
    expect(!result.ok && result.reason).toContain('product barcode')
  })
})
