import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { ReverseForm } from './ReverseForm'

describe('ReverseForm', () => {
  it('blocks a reason shorter than 5 characters and never calls the server', async () => {
    const onSubmit = vi.fn()
    render(<ReverseForm onSubmit={onSubmit} />)
    await userEvent.type(screen.getByLabelText('Reason (required)'), '  ab  ') // spaces do not count
    await userEvent.click(screen.getByRole('button', { name: 'Reverse this row' }))

    expect(await screen.findByText('Give a reason of at least 5 characters')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('sends the trimmed reason when it is long enough', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    render(<ReverseForm onSubmit={onSubmit} />)
    await userEvent.type(screen.getByLabelText('Reason (required)'), '  Scanned by mistake ')
    await userEvent.click(screen.getByRole('button', { name: 'Reverse this row' }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith('Scanned by mistake'))
  })

  it('shows a rule error from the server (e.g. the row is no longer reversible) in an alert', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiError({ status: 422, title: 'Business rule violated', detail: 'Unit X has been delivered since this receive.' }))
    render(<ReverseForm onSubmit={onSubmit} />)
    await userEvent.type(screen.getByLabelText('Reason (required)'), 'Wrong dock')
    await userEvent.click(screen.getByRole('button', { name: 'Reverse this row' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('has been delivered since this receive')
  })
})
