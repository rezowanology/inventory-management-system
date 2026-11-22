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
