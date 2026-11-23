import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { configApi, dashboardApi } from '../api/endpoints'
import { useAuth } from '../auth/auth-context'
import { ErrorAlert } from '../components/Alerts'
import { CAP, can } from '../lib/capabilities'
import { formatDateTime } from '../lib/dates'

// Quick-action buttons: each one is shown only when the server put its capability in /api/auth/me.
const ACTIONS = [
  { label: 'Track components', to: '/ops/track', capability: CAP.opsTrack },
  { label: 'Receive units', to: '/ops/receive', capability: CAP.opsReceive },
  { label: 'Deliver units', to: '/ops/deliver', capability: CAP.opsDeliver },
  { label: 'View history', to: '/history', capability: CAP.historyView },
  { label: 'Stock report', to: '/reports/stock', capability: CAP.reportsView },
  { label: 'Summary report', to: '/reports/summary', capability: CAP.reportsView },
  { label: 'Manage users', to: '/manage/users', capability: CAP.users },
  { label: 'Audit log', to: '/audit', capability: CAP.audit },
]

/** One Bootstrap stat card. `testId` lets the tests find the number. */
function StatCard({ label, value, testId, tone = 'secondary' }: { label: string; value: number | undefined; testId: string; tone?: string }) {
  return (
    <div className="col-6 col-lg-4 col-xl-2">
      <div className={`card text-center border-${tone} h-100`}>
        <div className="card-body py-3">
          <div className="text-secondary small">{label}</div>
          <div className="display-6" data-testid={testId}>{value ?? '-'}</div>
        </div>
      </div>
    </div>
  )
}

export function HomePage() {
  const { user } = useAuth()
  // useQuery fetches and caches server data; ['dashboard'] is the cache key (scans and reversals invalidate it).
  const dashboard = useQuery({ queryKey: ['dashboard'], queryFn: dashboardApi.get })
  const config = useQuery({ queryKey: ['config'], queryFn: configApi.get, staleTime: Infinity })
  if (!user) return null

  const d = dashboard.data
  const timeZone = config.data?.timeZone ?? 'UTC'
  const canReport = can(user, CAP.reportsView)

  return (
    <>
      <div className="card mb-4">
        <div className="card-body">
          <h1 className="h4">Welcome, {user.displayName}</h1>
          <p className="text-secondary">
            Signed in as <span className="badge text-bg-secondary">{user.role}</span> (employee {user.employeeNo}).
          </p>
          <div className="d-flex flex-wrap gap-2">
            {ACTIONS.filter((a) => can(user, a.capability)).map((a) => (
              <Link key={a.to} to={a.to} className="btn btn-outline-primary">{a.label}</Link>
            ))}
          </div>
        </div>
      </div>

      <ErrorAlert error={dashboard.error} />
      <div className="row g-3 mb-4">
        <StatCard label="In stock now" value={d?.stockCount} testId="stock-count" tone="success" />
        <StatCard label="Received today" value={d?.todayReceived} testId="today-received" tone="success" />
        <StatCard label="Delivered today" value={d?.todayDelivered} testId="today-delivered" tone="primary" />
        <StatCard label="Reversed today" value={d?.todayReversed} testId="today-reversed" tone="danger" />
        <StatCard label="Registered today" value={d?.unitsRegisteredToday} testId="today-registered" tone="info" />
        <StatCard label="Total units" value={d?.totalUnits} testId="total-units" />
      </div>

      <div className="row g-3">
        <div className="col-lg-5">
          <div className="card h-100">
            <div className="card-header">Stock by receive point</div>
            <div className="card-body p-0">
              <table className="table table-sm mb-0">
                <thead><tr><th>Receive point</th><th className="text-end">Units</th></tr></thead>
                <tbody>
                  {d?.stockByPoint.map((p) => <tr key={p.pointId}><td>{p.pointName}</td><td className="text-end">{p.count}</td></tr>)}
                  {d?.stockByPoint.length === 0 && <tr><td colSpan={2} className="text-secondary text-center py-3">Nothing in stock.</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div className="col-lg-7">
          <div className="card h-100">
            <div className="card-header d-flex justify-content-between align-items-center">
              <span>Stuck in stock the longest</span>
              {/* the stock report is sorted by days in stock, longest first, by default */}
              {canReport && <Link to="/reports/stock" className="small">Full stock report</Link>}
            </div>
            <div className="card-body p-0">
              <table className="table table-sm mb-0">
                <thead><tr><th>Barcode</th><th>Product</th><th>Receive point</th><th className="text-end">Days</th></tr></thead>
                <tbody>
                  {d?.stuckLongest.map((s) => (
                    <tr key={s.barcode}>
                      <td>{canReport ? <Link to={`/reports/barcode?barcode=${encodeURIComponent(s.barcode)}`}><code>{s.barcode}</code></Link> : <code>{s.barcode}</code>}</td>
                      <td>{s.productName}</td><td>{s.pointName}</td><td className="text-end">{s.daysInStock}</td>
                    </tr>
                  ))}
                  {d?.stuckLongest.length === 0 && <tr><td colSpan={4} className="text-secondary text-center py-3">Nothing in stock.</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        {/* Recent activity is only sent to roles that may see the history. */}
        {d && d.recent.length > 0 && (
          <div className="col-12">
            <div className="card">
              <div className="card-header d-flex justify-content-between align-items-center">
                <span>Recent activity</span>
                <Link to="/history" className="small">Full history</Link>
              </div>
              <div className="card-body p-0">
                <table className="table table-sm mb-0">
                  <thead><tr><th>Time</th><th>Barcode</th><th>Event</th><th>Point</th><th>By</th></tr></thead>
                  <tbody>
                    {d.recent.map((r) => (
                      <tr key={r.id}>
                        <td className="text-nowrap">{formatDateTime(r.occurredAt, timeZone)}</td><td><code>{r.unitBarcode}</code></td>
                        <td>{r.eventType}</td><td>{r.pointName}</td><td>{r.performedByEmployeeNo} {r.performedByName}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}
      </div>
    </>
  )
}
