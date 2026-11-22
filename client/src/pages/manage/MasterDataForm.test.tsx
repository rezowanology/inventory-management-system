import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import { MasterDataForm } from './MasterDataForm'

describe('MasterDataForm', () => {
  it('shows the zod rule under the name field before calling the server', async () => {
    const onSubmit = vi.fn()
    render(<MasterDataForm singular="Product" hasType={false} onSubmit={onSubmit} onClose={() => {}} />)
    await userEvent.type(screen.getByLabelText('Name'), ' a ')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('The name must have at least 2 characters')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('puts a server field error under the name input', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiError({ status: 422, title: 'Business rule violated', fieldErrors: { name: ['"Compressor" already exists.'] } }))
    render(<MasterDataForm singular="Component" hasType={false} onSubmit={onSubmit} onClose={() => {}} />)
    await userEvent.type(screen.getByLabelText('Name'), ' compressor ')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('"Compressor" already exists.')).toBeInTheDocument()
    expect(screen.getByLabelText('Name')).toHaveClass('is-invalid')
    expect(onSubmit).toHaveBeenCalledWith({ name: 'compressor', type: undefined }) // trimmed before sending
  })

  it('requires a type for points and sends it', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    render(<MasterDataForm singular="Point" hasType onSubmit={onSubmit} onClose={() => {}} />)
    await userEvent.type(screen.getByLabelText('Name'), 'Dock 1')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Choose a type')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()

    await userEvent.selectOptions(screen.getByLabelText('Type'), 'RECEIVE')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith({ name: 'Dock 1', type: 'RECEIVE' }))
  })
})
