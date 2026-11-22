import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { configApi, masterDataApi } from '../../api/endpoints'
import type { MasterDataInput, MasterDataRow } from '../../api/types'
import { ErrorAlert } from '../../components/Alerts'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { DataTable, type Column } from '../../components/DataTable'
import { ExportButtons } from '../../components/ExportButtons'
import { ListToolbar } from '../../components/ListToolbar'
import { Pagination } from '../../components/Pagination'
import { toQueryString, useListParams } from '../../hooks/useListParams'
import { formatDateTime } from '../../lib/dates'
import { POINT_TYPES, type MasterKindConfig } from './constants'
import { MasterDataForm } from './MasterDataForm'

const PAGE_SIZE = 20

/** One list screen for products, components and points (they differ only by the config passed in). */
export function MasterDataPage({ config }: { config: MasterKindConfig }) {
  const { kind, title, singular, hasType } = config
  const queryClient = useQueryClient()
  // q, page, status and type live in the URL (?q=&page=&status=&type=) so Back and shared links work.
  const { params, setQ, setFilter, setPage } = useListParams(['status', 'type'], { status: 'active' })
  const [editing, setEditing] = useState<MasterDataRow | 'new' | null>(null)
  const [deleting, setDeleting] = useState<MasterDataRow | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const query = toQueryString({
    q: params.q, page: params.page, pageSize: PAGE_SIZE,
    status: params.filters.status, type: hasType ? params.filters.type : undefined,
  })
  // The React Query key = area + parameters. A new page or filter is a new key, so it is fetched (and cached) separately.
  const list = useQuery({ queryKey: ['master', kind, query], queryFn: () => masterDataApi.list(kind, query) })
  const appConfig = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  const timeZone = appConfig.data?.timeZone ?? 'UTC'

  // After every change, mark this area's lists (and the dropdown options) as out of date so they reload.
  function refresh() {
    void queryClient.invalidateQueries({ queryKey: ['master', kind] })
    void queryClient.invalidateQueries({ queryKey: ['options', kind] })
  }

  const save = useMutation({
    mutationFn: (input: MasterDataInput) =>
      editing && editing !== 'new' ? masterDataApi.update(kind, editing.id, input) : masterDataApi.create(kind, input),
    onSuccess: () => {
      setNotice(`${singular} ${editing === 'new' ? 'created' : 'saved'}.`)
      setEditing(null)
      refresh()
    },
  })

  const remove = useMutation({
    mutationFn: (row: MasterDataRow) => masterDataApi.remove(kind, row.id),
    onSuccess: (result, row) => {
      setNotice(result.archived
        ? `In use: "${row.name}" was archived instead of deleted. It is hidden from dropdowns but still shown in history, and can be restored.`
        : `"${row.name}" was deleted.`)
      setDeleting(null)
      refresh()
    },
    onError: () => setDeleting(null),
  })

  const restore = useMutation({
    mutationFn: (row: MasterDataRow) => masterDataApi.restore(kind, row.id),
    onSuccess: (_, row) => { setNotice(`"${row.name}" was restored.`); refresh() },
  })

  const columns: Column<MasterDataRow>[] = [
    { header: 'Name', cell: (r) => r.name },
    ...(hasType ? [{ header: 'Type', cell: (r: MasterDataRow) => <span className="badge text-bg-info">{r.pointType}</span> }] : []),
    {
      header: 'Status',
      cell: (r) => r.isDeleted
        ? <span className="badge text-bg-secondary" title={r.deletedAt ? `Archived ${formatDateTime(r.deletedAt, timeZone)}` : ''}>Archived</span>
        : <span className="badge text-bg-success">Active</span>,
    },
    { header: 'Updated', cell: (r) => formatDateTime(r.updatedAt, timeZone) },
    {
      header: '', className: 'text-end text-nowrap',
      cell: (r) => (
        <>
          {r.isDeleted
            ? <button type="button" className="btn btn-sm btn-outline-success" onClick={() => restore.mutate(r)}>Restore</button>
            : <button type="button" className="btn btn-sm btn-outline-primary" onClick={() => setEditing(r)}>Edit</button>}
          <button type="button" className="btn btn-sm btn-outline-danger ms-2" onClick={() => setDeleting(r)}>Delete</button>
        </>
      ),
    },
  ]

  const mutationError = remove.error ?? restore.error

  return (
    <>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h4 mb-0">{title}</h1>
        <button type="button" className="btn btn-primary" onClick={() => { save.reset(); setEditing('new') }}>New {singular.toLowerCase()}</button>
      </div>

      {notice && <div className="alert alert-success alert-dismissible" role="status">
        {notice}<button type="button" className="btn-close" aria-label="Close" onClick={() => setNotice(null)} />
      </div>}
      <ErrorAlert error={mutationError} />
      <ErrorAlert error={list.error} />

      <ListToolbar q={params.q} onSearch={setQ} placeholder={`Search ${title.toLowerCase()}...`}
        exportSlot={<ExportButtons path={`/api/${kind}`} query={query} />}>
        <select className="form-select w-auto" aria-label="Status filter" value={params.filters.status}
          onChange={(e) => setFilter('status', e.target.value)}>
          <option value="active">Active</option>
          <option value="deleted">Archived</option>
          <option value="all">All</option>
        </select>
        {hasType && (
          <select className="form-select w-auto" aria-label="Type filter" value={params.filters.type}
            onChange={(e) => setFilter('type', e.target.value)}>
            <option value="">All types</option>
            {POINT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
        )}
      </ListToolbar>

      <DataTable columns={columns} rows={list.data?.items} rowKey={(r) => r.id} loading={list.isPending}
        rowClass={(r) => (r.isDeleted ? 'text-secondary' : undefined)} emptyText={`No ${title.toLowerCase()} found.`} />
      <Pagination page={params.page} pageSize={PAGE_SIZE} totalCount={list.data?.totalCount ?? 0} onPage={setPage} />

      {editing && (
        <MasterDataForm singular={singular} hasType={hasType} editing={editing === 'new' ? undefined : editing}
          onSubmit={(values) => save.mutateAsync(values)} onClose={() => setEditing(null)} />
      )}
      {deleting && (
        <ConfirmDialog title={`Delete ${singular.toLowerCase()}`} confirmLabel="Delete" busy={remove.isPending}
          onConfirm={() => remove.mutate(deleting)} onCancel={() => setDeleting(null)}>
          Delete <strong>{deleting.name}</strong>? If it is already used in the inventory records it will be archived
          instead (hidden from dropdowns, still shown in history, restorable). Otherwise it is removed for good.
        </ConfirmDialog>
      )}
    </>
  )
}
