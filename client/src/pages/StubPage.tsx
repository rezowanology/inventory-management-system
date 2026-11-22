/** Placeholder for a screen that is built on a later day. It still sits behind its real route guard. */
export function StubPage({ title, day }: { title: string; day: number }) {
  return (
    <div className="card">
      <div className="card-body">
        <h1 className="h4">{title}</h1>
        <p className="text-secondary mb-0">Built on Day {day}.</p>
      </div>
    </div>
  )
}
