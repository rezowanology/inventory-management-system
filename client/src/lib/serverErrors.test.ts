import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import { applyServerErrors } from './serverErrors'

describe('applyServerErrors', () => {
  it('puts each server field error on the matching form field', () => {
    const setError = vi.fn()
    const error = new ApiError({
      status: 422, title: 'x', fieldErrors: { name: ['Already exists'], EmployeeNo: ['Taken'], other: ['ignored'] },
    })
    const rest = applyServerErrors(error, setError, ['name', 'employeeNo'])
    expect(setError).toHaveBeenCalledWith('name', { type: 'server', message: 'Already exists' })
    expect(setError).toHaveBeenCalledWith('employeeNo', { type: 'server', message: 'Taken' }) // "EmployeeNo" matched too
    expect(setError).toHaveBeenCalledTimes(2)
    expect(rest).toBeNull()
  })

  it('returns the general message when no field matched', () => {
    const setError = vi.fn()
    const error = new ApiError({ status: 422, title: 'Business rule violated', detail: 'You cannot deactivate your own account.' })
    expect(applyServerErrors(error, setError, ['name'])).toBe('You cannot deactivate your own account.')
    expect(setError).not.toHaveBeenCalled()
  })
})

describe('applyServerErrors with array rows', () => {
  it('maps "components[1].componentBarcode" onto the react-hook-form path "components.1.componentBarcode"', () => {
    const setError = vi.fn()
    const error = new ApiError({
      status: 422, title: 'x', fieldErrors: { 'components[1].componentBarcode': ['Component X is already attached to unit Y'] },
    })
    const rest = applyServerErrors(error, setError, ['components.0.componentBarcode', 'components.1.componentBarcode'])
    expect(setError).toHaveBeenCalledWith('components.1.componentBarcode', { type: 'server', message: 'Component X is already attached to unit Y' })
    expect(rest).toBeNull()
  })
})
