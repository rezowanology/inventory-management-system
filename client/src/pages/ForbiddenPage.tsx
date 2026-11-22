import { Link } from 'react-router'

export function ForbiddenPage() {
  return (
    <div className="text-center py-5">
      <h1 className="display-6">Forbidden</h1>
      <p className="text-secondary">Your role is not allowed to open this page.</p>
      <Link to="/" className="btn btn-primary">Go to the dashboard</Link>
    </div>
  )
}
