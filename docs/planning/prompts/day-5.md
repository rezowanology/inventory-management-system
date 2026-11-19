(Project context: CLAUDE.md in this folder is loaded automatically and is the authoritative
project specification. Follow it. The full plan is docs/planning/IMS_Build_Plan.md.)

=== DAY 5 OF 6: AUDIT LOG, DASHBOARD, DEMO DATA, FINAL DIAGRAMS + SQL SCRIPT ===

1. ROLE
You are a senior full-stack engineer and data modeller finishing the feature set and the design
deliverables. Think like the reviewer who will compare the ER diagram with the database.

2. CONTEXT & CURRENT STATE
Days 1–4 are done (tags day-1..day-4): Identity cookie + policies, admin panel (soft delete,
activation, forced logout, password change), Track/Receive/Deliver with duplicate-scan warnings
and DB safety-net trigger, admin reversals with correct side effects, the three reports with a
chart, CSV/Excel export. The audit API and /audit page are stubs; the dashboard only shows two
counts; the diagrams are Day 1 drafts.
MUST NOT REGRESS: every rule/policy test, exports, reversal semantics, the sample record.
Starting-state check:
    git checkout main && git pull ; git tag --list "day-*"  -> day-1..day-4
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Clear any Carry-over items in DECISIONS.md first.

3. TODAY'S OBJECTIVE
Complete the remaining additional requirements (audit log, dashboard, search/filter/pagination
everywhere), add deterministic demo data, and finalise the flow diagram, ER diagram and SQL
script. Advances: Additional 1 (sweep), 3, 6; Deliverables: flow diagram, ER + SQL.

4. ORDERED TASK LIST
 1. AuditInterceptor (SaveChangesInterceptor): for Added/Modified/Deleted Product, Component,
    Point and ApplicationUser entries, add AuditLog rows in the same SaveChanges: CREATE / UPDATE /
    DELETE with only changed properties in old_values/new_values (System.Text.Json). Map is_deleted
    to SOFT_DELETE/RESTORE, role to ROLE_CHANGE, is_active to ACTIVATE/DEACTIVATE. NEVER log
    PasswordHash, SecurityStamp, ConcurrencyStamp or Normalized* columns; password changes are
    PASSWORD_CHANGE / PASSWORD_RESET with no values. Actor via IHttpContextAccessor (null for the
    seeder = "system"); IP from HttpContext.Connection.RemoteIpAddress.
 2. AuthController writes LOGIN, LOGIN_FAILED (attempted username only) and LOGOUT. audit_log is
    append-only already; add a test proving it.
 3. Audit API (audit.view) with the list contract: filters actor, action, entity type, date range;
    search; paging; export; GET /api/audit/{id} with old/new values. React /audit: table plus an
    expandable row showing old vs new values side by side.
 4. Dashboard: DashboardService.GetMetricsAsync(today in App:TimeZone): current stock count,
    today's receives, deliveries and reversals, units registered today; stock by receive point;
    top 10 InStock units by days in stock; last 10 history rows (only returned to history.view
    roles). React dashboard: Bootstrap stat cards, a stock-by-point table, the "Stuck in stock
    the longest" list (barcode links to /reports/barcode for reports.view roles; plain text
    otherwise) with a link to /reports/stock sorted by days, recent activity, and quick-action
    buttons only for capabilities in me.capabilities. Tests for the metrics.
 5. Search/filter/pagination sweep: confirm EVERY list uses the shared contract and components with
    search, filters, paging and (where sensible) export: products, components, points, users,
    history, stock report, summary table, barcode timeline (filter by event type), audit log.
    Fix gaps; put the checklist in DECISIONS.md.
 6. `-- seed --demo-data`: deterministic (new Random(42)); about 25 extra units DEMO000001… over
    the last 30 days across products/points/users, including units in stock 20+ days, some
    received and delivered today, one reversal of each type (through InventoryService so all
    rules apply), and one archived master-data item. Never touch the sample record.
 7. Final diagrams: docs/diagrams/process-flow.md (Mermaid flowchart with a swimlane per role and
    decision diamonds for the rules and the duplicate-scan warning; show the React -> API -> DB
    path once) and docs/diagrams/er-diagram.md (Mermaid erDiagram that EXACTLY matches the
    database, incl. the reverses_id self-reference; notes on append-only triggers, derived
    status, soft delete; mention Identity's extra users columns).
 8. SQL deliverable: docs/db/schema.sql = `dotnet ef migrations script --idempotent --project
    src/Ims.Api -o docs/db/schema.sql`; scripts/export-sql.ps1 reads the connection string from
    user-secrets INSIDE the script (never printing it), sets PGPASSWORD for the child process
    only, and runs `pg_dump --no-owner --no-privileges` of a freshly reset + migrated + `-- seed`
    database to docs/db/inventory_with_sample_data.sql; docs/db/README.md explains loading and
    regenerating.
 9. Integration test SampleRecordEndToEnd: from seed, assert the 3 rows in order, events/points/
    users (employee_no) match the brief, status Delivered, barcode report = 3 rows, summary for
    2026-03-27..30 = 1/1/1.
10. Tests for audit (create/update diff, soft delete, role change, deactivate, login, failed
    login, no password data anywhere, append-only). LEARNING_NOTES.md Day 5 (EF Core interceptors
    and the ChangeTracker, IHttpContextAccessor, System.Text.Json, migration scripts; React:
    composing dashboard cards, conditional rendering by capability).

5. ACCEPTANCE CRITERIA
- [ ] Editing a product name writes an audit row with the old and new name; admin sees it at
      /audit with the actor and time
- [ ] Login, logout and a failed login each appear in the audit log; no password data anywhere
- [ ] Role change and deactivate/activate appear with old -> new values
- [ ] Audit rows cannot be edited or deleted (tests)
- [ ] With --demo-data the dashboard shows non-zero tiles, a stock-by-point table and a "stuck
      longest" list; each role sees only their quick actions
- [ ] Every list/report in the sweep has search + filters + paging (+ export where relevant)
- [ ] The ER diagram matches the database one-to-one; the flow diagram shows all roles + reversals
- [ ] docs/db/schema.sql and docs/db/inventory_with_sample_data.sql exist and load into an empty
      PostgreSQL database without errors, with the 3 sample rows present
- [ ] The end-to-end sample-record test passes; all tests pass

6. VERIFICATION
    dotnet ef database drop --force --project src/Ims.Api
    dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed
    dotnet ef migrations script --idempotent --project src/Ims.Api -o docs/db/schema.sql
    powershell -ExecutionPolicy Bypass -File scripts/export-sql.ps1
    createdb -U ims_user ims_verify
    psql -U ims_user -d ims_verify -v ON_ERROR_STOP=1 -f docs/db/inventory_with_sample_data.sql
    psql -U ims_user -d ims_verify -c "select count(*) from inventory_transactions"   -> 3
    dropdb -U ims_user ims_verify
    dotnet run --project src/Ims.Api -- seed --demo-data
    dotnet test && npm test --prefix client && npm run build --prefix client   -> green
    demo mode -> http://localhost:5080: dashboard as admin, tracker, viewer
(createdb/psql/dropdb ask for the ims_user password: the intern types it.)

7. DEMO NOTE
Open the dashboard with demo data (tiles + stuck-longest list), then change a product name and show
the matching audit row with old/new values.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "feat(audit): audit log, dashboard metrics, demo data, final ER/flow diagrams and SQL script"
    git push origin main
    git tag -a day-5 -m "Day 5: feature complete"
    git push origin day-5

9. GUARDRAILS
- Do not break prior features; exports, reversals and operations tests must stay green.
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- audit_log is separate from inventory history; never mix them.
- Demo data must be deterministic and must never modify the brief's sample record.
