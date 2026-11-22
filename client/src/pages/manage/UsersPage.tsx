import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { configApi, usersApi } from '../../api/endpoints'
import type { CreateUserInput, UpdateUserInput, UserRow } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { DataTable, type Column } from '../../components/DataTable'
import { ExportButtons } from '../../components/ExportButtons'
import { ListToolbar } from '../../components/ListToolbar'
import { Pagination } from '../../components/Pagination'
import { toQueryString, useListParams } from '../../hooks/useListParams'
import { formatDateTime } from '../../lib/dates'
import { ResetPasswordDialog } from './ResetPasswordDialog'
import { ROLES } from './constants'
import { UserForm } from './UserForm'

const PAGE_SIZE = 20

function StatusBadge({ user }: { user: UserRow }) {
  if (user.isPending) return <span className="badge text-bg-warning">Pending registration</span>
  return user.isActive ? <span className="badge text-bg-success">Active</span> : <span className="badge text-bg-secondary">Inactive</span>
}

export function UsersPage() {
  const queryClient = useQueryClient()
  const { params, setQ, setFilter, setPage } = useListParams(['role', 'active', 'pending'])
  const [editing, setEditing] = useState<UserRow | 'new' | null>(null)
  const [deactivating, setDeactivating] = useState<UserRow | null>(null)
  const [resetting, setResetting] = useState<UserRow | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const query = toQueryString({
    q: params.q, page: params.page, pageSize: PAGE_SIZE,
    role: params.filters.role, active: params.filters.active, pending: params.filters.pending,
  })
  const list = useQuery({ queryKey: ['users', query], queryFn: () => usersApi.list(query) })
  const appConfig = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  const timeZone = appConfig.data?.timeZone ?? 'UTC'

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['users'] })

  const create = useMutation({
    mutationFn: (input: CreateUserInput) => usersApi.create(input),
    onSuccess: (u) => { setNotice(`User "${u.userName}" was created.`); setEditing(null); void refresh() },
  })
  const update = useMutation({
    mutationFn: ({ id, input }: { id: number; input: UpdateUserInput }) => usersApi.update(id, input),
    onSuccess: (u) => { setNotice(`User "${u.userName}" was saved.`); setEditing(null); void refresh() },
  })
  const toggle = useMutation({
    mutationFn: (u: UserRow) => usersApi.toggleActive(u.id),
    onSuccess: (u) => {
      setNotice(u.isActive ? `"${u.userName}" is active again.` : `"${u.userName}" was deactivated and is signed out.`)
      setDeactivating(null)
      void refresh()
    },
    onError: () => setDeactivating(null), // the reason (self / last admin) shows in the red alert
  })
  const reset = useMutation({
    mutationFn: ({ id, password }: { id: number; password: string }) => usersApi.resetPassword(id, password),
    onSuccess: () => { setNotice('The password was changed.'); setResetting(null) },
  })

  const columns: Column<UserRow>[] = [
    { header: 'User name', cell: (u) => <code>{u.userName}</code> },
    { header: 'Name', cell: (u) => `${u.firstName} ${u.lastName}` },
    { header: 'Employee no.', cell: (u) => u.employeeNo },
    { header: 'Role', cell: (u) => <span className="badge text-bg-info">{u.role}</span> },
    { header: 'Status', cell: (u) => <StatusBadge user={u} /> },
    { header: 'Last sign-in', cell: (u) => (u.lastLoginAt ? formatDateTime(u.lastLoginAt, timeZone) : 'never') },
    {
      header: '', className: 'text-end text-nowrap',
      cell: (u) => (
        <>
          <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => { create.reset(); update.reset(); setEditing(u) }}>Edit</button>
          <button type="button" className="btn btn-sm btn-outline-secondary ms-2" onClick={() => { reset.reset(); setResetting(u) }}>Reset password</button>
          {u.isActive
            ? <button type="button" className="btn btn-sm btn-outline-danger ms-2" onClick={() => setDeactivating(u)}>Deactivate</button>
            : <button type="button" className="btn btn-sm btn-outline-success ms-2" onClick={() => toggle.mutate(u)}>Activate</button>}
        </>
      ),
    },
  ]

  const pendingOn = params.filters.pending === 'true'

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h4 mb-0">Users</h1>
        <button type="button" className="btn btn-primary" onClick={() => { create.reset(); setEditing('new') }}>New user</button>
      </div>

      {notice && <div className="alert alert-success alert-dismissible" role="status">
        {notice}<button type="button" className="btn-close" aria-label="Close" onClick={() => setNotice(null)} />
      </div>}
      <ErrorAlert error={toggle.error} />
      <ErrorAlert error={list.error} />

      <ListToolbar q={params.q} onSearch={setQ} placeholder="Search user name, name or employee no..."
        exportSlot={<ExportButtons path="/api/users" query={query} />}>
        <select className="form-select w-auto" aria-label="Role filter" value={params.filters.role}
          onChange={(e) => setFilter('role', e.target.value)}>
          <option value="">All roles</option>
          {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
        <select className="form-select w-auto" aria-label="Active filter" value={params.filters.active}
          onChange={(e) => setFilter('active', e.target.value)}>
          <option value="">Active and inactive</option>
          <option value="true">Active only</option>
          <option value="false">Inactive only</option>
        </select>
        {/* A "chip": a toggle button that looks like a tag. Pressed = only users waiting for activation. */}
        <button type="button" aria-pressed={pendingOn}
          className={`btn btn-sm rounded-pill ${pendingOn ? 'btn-warning' : 'btn-outline-warning'}`}
          onClick={() => setFilter('pending', pendingOn ? '' : 'true')}>Pending registration</button>
      </ListToolbar>

      <DataTable columns={columns} rows={list.data?.items} rowKey={(u) => u.id} loading={list.isPending}
        rowClass={(u) => (u.isActive ? undefined : 'text-secondary')} emptyText="No users found." />
      <Pagination page={params.page} pageSize={PAGE_SIZE} totalCount={list.data?.totalCount ?? 0} onPage={setPage} />

      {editing && (
        <UserForm editing={editing === 'new' ? undefined : editing}
          onCreate={(input) => create.mutateAsync(input)}
          onUpdate={(id, input) => update.mutateAsync({ id, input })}
          onClose={() => setEditing(null)} />
      )}
      {deactivating && (
        <ConfirmDialog title="Deactivate user" confirmLabel="Deactivate" busy={toggle.isPending}
          onConfirm={() => toggle.mutate(deactivating)} onCancel={() => setDeactivating(null)}>
          Deactivate <strong>{deactivating.userName}</strong>? They are signed out on their next action and cannot
          sign in again until an Admin activates the account.
        </ConfirmDialog>
      )}
      {resetting && (
        <ResetPasswordDialog user={resetting} onClose={() => setResetting(null)}
          onSubmit={(password) => reset.mutateAsync({ id: resetting.id, password })} />
      )}
    </>
  )
}
