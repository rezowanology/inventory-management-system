import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { dashboardApi } from '../api/endpoints'
import { useAuth } from '../auth/auth-context'
import { ErrorAlert } from '../components/Alerts'
import { CAP, can } from '../lib/capabilities'

const ACTIONS = [
  { label: 'Track components', to: '/ops/track', capability: CAP.opsTrack },
  { label: 'Receive units', to: '/ops/receive', capability: CAP.opsReceive },
  { label: 'Deliver units', to: '/ops/deliver', capability: CAP.opsDeliver },
  { label: 'View history', to: '/history', capability: CAP.historyView },
  { label: 'Stock report', to: '/reports/stock', capability: CAP.reportsView },
  { label: 'Manage users', to: '/manage/users', capability: CAP.users },
]

export function HomePage() {
  const { user } = useAuth()
  // useQuery fetches and caches server data; ['dashboard'] is the cache key.
  const dashboard = useQuery({ queryKey: ['dashboard'], queryFn: dashboardApi.get })
  if (!user) return null

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
      <div className="row g-3">
        <div className="col-sm-6">
          <div className="card text-center"><div className="card-body">
            <div className="text-secondary">Total units</div>
            <div className="display-6" data-testid="total-units">{dashboard.data?.totalUnits ?? '-'}</div>
          </div></div>
        </div>
        <div className="col-sm-6">
          <div className="card text-center"><div className="card-body">
            <div className="text-secondary">In stock now</div>
            <div className="display-6" data-testid="stock-count">{dashboard.data?.stockCount ?? '-'}</div>
          </div></div>
        </div>
      </div>
    </>
  )
}
