import { Link, Outlet, useNavigate } from 'react-router'
import { useAuth } from '../auth/auth-context'
import { CAP, can } from '../lib/capabilities'

interface MenuItem { label: string; to: string; capability: string }
interface MenuGroup { label: string; items: MenuItem[] }

// The menu is declared once; each item is shown only when the server says the user has its capability.
const MENU: MenuGroup[] = [
  { label: 'Operations', items: [
    { label: 'Track components', to: '/ops/track', capability: CAP.opsTrack },
    { label: 'Receive', to: '/ops/receive', capability: CAP.opsReceive },
    { label: 'Deliver', to: '/ops/deliver', capability: CAP.opsDeliver },
  ] },
  { label: 'Records', items: [
    { label: 'History', to: '/history', capability: CAP.historyView },
  ] },
  { label: 'Reports', items: [
    { label: 'Stock', to: '/reports/stock', capability: CAP.reportsView },
    { label: 'Summary', to: '/reports/summary', capability: CAP.reportsView },
    { label: 'Barcode history', to: '/reports/barcode', capability: CAP.reportsView },
  ] },
  { label: 'Admin', items: [
    { label: 'Products', to: '/manage/products', capability: CAP.masterData },
    { label: 'Components', to: '/manage/components', capability: CAP.masterData },
    { label: 'Points', to: '/manage/points', capability: CAP.masterData },
    { label: 'Users', to: '/manage/users', capability: CAP.users },
    { label: 'Audit log', to: '/audit', capability: CAP.audit },
  ] },
]

/** Page frame: Bootstrap navbar (role-aware) + the routed page in <Outlet />. */
export function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  if (!user) return null

  async function handleLogout() {
    await logout()
    navigate('/login')
  }

  return (
    <>
      <nav className="navbar navbar-expand-lg navbar-dark bg-dark">
        <div className="container-fluid">
          <Link className="navbar-brand" to="/">IMS</Link>
          <button className="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#mainNav"
            aria-controls="mainNav" aria-expanded="false" aria-label="Toggle navigation">
            <span className="navbar-toggler-icon" />
          </button>
          <div className="collapse navbar-collapse" id="mainNav">
            <ul className="navbar-nav me-auto">
              {MENU.map((group) => {
                const items = group.items.filter((i) => can(user, i.capability))
                if (items.length === 0) return null // hide groups with nothing allowed
                return (
                  <li className="nav-item dropdown" key={group.label}>
                    <a className="nav-link dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown"
                      aria-expanded="false" onClick={(e) => e.preventDefault()}>{group.label}</a>
                    <ul className="dropdown-menu">
                      {items.map((i) => (
                        <li key={i.to}><Link className="dropdown-item" to={i.to}>{i.label}</Link></li>
                      ))}
                    </ul>
                  </li>
                )
              })}
            </ul>
            <ul className="navbar-nav">
              <li className="nav-item dropdown">
                <a className="nav-link dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown"
                  aria-expanded="false" onClick={(e) => e.preventDefault()}>
                  {user.displayName} <span className="badge text-bg-secondary ms-1">{user.role}</span>
                </a>
                <ul className="dropdown-menu dropdown-menu-end">
                  <li><span className="dropdown-item-text small text-secondary">Employee no. {user.employeeNo}</span></li>
                  <li><hr className="dropdown-divider" /></li>
                  <li><Link className="dropdown-item" to="/change-password">Change password</Link></li>
                  <li><button className="dropdown-item" type="button" onClick={handleLogout}>Logout</button></li>
                </ul>
              </li>
            </ul>
          </div>
        </div>
      </nav>
      <main className="container py-4">
        <Outlet />
      </main>
    </>
  )
}
