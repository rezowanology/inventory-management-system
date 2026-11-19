=== PROJECT CONTEXT BLOCK: Inventory Management System (IMS), React + .NET 10 API + PostgreSQL ===

PROJECT
A barcode-based inventory system (learning/demo project, industrial attachment) that tracks one
physical unit (product barcode) from component tracking -> receiving -> delivery, with
role-based access and reports. Goal: a working, presentable end-to-end demo, NOT production
hardening. No over-engineering, no extra infrastructure.
The intern is LEARNING C#, .NET and React this week: write clear, idiomatic code (modern C# with
nullable enabled and async/await; React function components with hooks and strict TypeScript),
short XML doc comments on C# services, and a one-line comment wherever a framework idiom appears
(DI registration, policies, interceptors, React Query keys, form resolvers). No clever
abstractions, no generic repositories, no global state library.

ARCHITECTURE (fixed for all days; do not change it)
- client/  React single-page app: Vite + React 19 + TypeScript (strict). React Router (declarative
  routes), TanStack Query for server data, react-hook-form + zod for forms, Bootstrap 5 CSS + its
  JS bundle (no React UI kit), Chart.js via react-chartjs-2. Vitest + React Testing Library.
  Keep the versions create-vite and npm resolve on Day 1 and record them in README.
- src/Ims.Api/  ASP.NET Core Web API on .NET 10 (controllers with [ApiController]), ASP.NET Core
  Identity, EF Core 10 + Npgsql.EntityFrameworkCore.PostgreSQL 10.x + EFCore.NamingConventions
  (snake_case), ClosedXML for .xlsx. Returns JSON (camelCase) and RFC 7807 ProblemDetails errors.
- ONE ORIGIN: in development Vite (port 5173) proxies /api to the API (port 5080); for the demo
  `npm run build` writes into src/Ims.Api/wwwroot and the API serves it with UseStaticFiles +
  MapFallbackToFile("index.html"). No CORS configuration.
- AUTH: Identity application cookie (HttpOnly, SameSite=Strict, Secure only outside Development).
  For /api paths the cookie events return 401/403 instead of redirects. Antiforgery: header
  "X-XSRF-TOKEN"; GET /api/auth/csrf stores a readable XSRF-TOKEN cookie via IAntiforgery; a
  global AutoValidateAntiforgeryToken filter checks every unsafe method; the client re-fetches
  the token after login and logout. NEVER store tokens or passwords in localStorage.
- PostgreSQL 17 on localhost:5432. Connection string ONLY in .NET user-secrets of Ims.Api under
  "ConnectionStrings:Ims" (never in appsettings*.json, never in git, never printed).
- Tests: xUnit + Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory<Program>) against a real
  PostgreSQL database `ims_test`; Vitest for client logic and components. No EF in-memory
  provider, no SQLite. Day 6: Playwright (Node) for screenshots and diagram images.

REPO LAYOUT
  Ims.sln, global.json (SDK 10.0.404, rollForward latestFeature), Directory.Build.props
  (Nullable + ImplicitUsings), .gitignore (dotnet + node: bin/, obj/, node_modules/,
  src/Ims.Api/wwwroot/, playwright output), README.md, DECISIONS.md
  src/Ims.Api/
    Program.cs                    composition root (services, auth, policies, interceptors, seed arg)
    Domain/                       ApplicationUser, Product, Component, Point, Unit,
                                  InventoryTransaction, AuditLog, enums Role, PointType, EventType
    Data/ImsDbContext.cs, Data/Configurations/, Data/Migrations/, Data/Seed/DbSeeder.cs
    Authorization/Roles.cs, Capabilities.cs (policy names + capability->roles map),
    ImsClaimsPrincipalFactory.cs, ImsSignInManager.cs
    Contracts/                    request/response records (DTOs) per area; never expose entities
    Controllers/                  Auth, Config, Units (lookup), Ops, History, Reports, Products,
                                  Components, Points, Users, Audit, Dashboard
    Services/                     InventoryService (ALL inventory rules), ReportService,
                                  DashboardService, ExportService, UserAdminService,
                                  AuditInterceptor, AppendOnlyInterceptor, Clock
    Infrastructure/               ListQuery, PagedResult<T>, ProblemDetails mapping for
                                  DomainException / DuplicateScanException
  client/
    vite.config.ts                proxy /api -> http://localhost:5080; build.outDir
                                  ../src/Ims.Api/wwwroot (emptyOutDir)
    src/main.tsx, src/App.tsx     providers + routes
    src/api/                      client.ts (fetch wrapper: credentials, X-XSRF-TOKEN, JSON,
                                  ProblemDetails -> ApiError), one file per area, types.ts
    src/auth/                     AuthProvider (GET /api/auth/me), useAuth, RequireAuth,
                                  RequireCapability
    src/components/               Layout, NavBar, DataTable, ListToolbar, Pagination,
                                  ExportButtons, StatusBadge, ScanPanel, ConfirmDialog, Alerts
    src/pages/                    one folder per area (see ROUTES)
    src/lib/                      barcode.ts (same regex as the server), dates.ts, capabilities.ts
  tests/Ims.Tests/                ImsApiFactory, TestAuthHandler, test classes per area
  tools/Ims.DocTools/             (Day 6) test-sheet builder (ClosedXML)
  docs/  diagrams/, db/, testing/, user-guide/, demo/, LEARNING_NOTES.md
  scripts/  export-sql.ps1 (Day 5); client/e2e/ (Day 6 Playwright scripts)

ROLES (exactly one per user: ApplicationUser.Role, an enum stored as text)
  ADMIN, COMPONENT_TRACKER, RECEIVE_USER, DELIVERY_USER, REPORT_VIEWER
  ImsClaimsPrincipalFactory adds the role as a ClaimTypes.Role claim plus "employee_no".
  Identity role tables are not used (IdentityUserContext).

PERMISSION MATRIX (single source of truth: src/Ims.Api/Authorization/Capabilities.cs. Program.cs
registers one policy per capability; every controller action carries [Authorize(Policy = ...)];
a FallbackPolicy requires an authenticated user. GET /api/auth/me returns the user's capability
list, and the React menu and RequireCapability guards use ONLY that list, so the client never
re-encodes the matrix. Forbidden API call -> 403; React shows the Forbidden page.)
  capability                 ADMIN  TRACKER  RECEIVE  DELIVERY  VIEWER
  dashboard (read-only)        Y       Y        Y        Y        Y
  change own password          Y       Y        Y        Y        Y
  ops.track                    Y       Y        -        -        -
  ops.receive                  Y       -        Y        -        -
  ops.deliver                  Y       -        -        Y        -
  ops.lookup                   Y       Y        Y        Y        -
  history.view                 Y       -        -        -        Y
  history.reverse              Y       -        -        -        -
  reports.view (+ export)      Y       -        -        -        Y
  masterdata.manage            Y       -        -        -        -
  users.manage                 Y       -        -        -        -
  audit.view                   Y       -        -        -        -

DATA MODEL (EF Core entities; snake_case tables; names are binding)
- users (ApplicationUser : IdentityUser<long>, table renamed "users"): Identity's columns plus
  first_name, last_name, employee_no (unique), role (text, default REPORT_VIEWER), is_active.
  Users are never hard-deleted.
- products, components: id, name, is_deleted, deleted_at, created_at, updated_at. Name unique
  case-insensitively among non-deleted rows: partial unique index on lower(name) WHERE NOT
  is_deleted, created with migrationBuilder.Sql.
- points: same + point_type (TRACK | RECEIVE | DELIVERY); unique (lower(name), point_type) among
  non-deleted rows.
- units: id, barcode (unique), product_id FK (restrict), registered_at, registered_by FK.
  Identity only: NO status column.
- inventory_transactions: id, unit_id FK, event_type (COMPONENT_TRACKED | RECEIVED | DELIVERED |
  REVERSED), point_id FK, component_id FK null, component_barcode null, reverses_id FK self null
  UNIQUE, reason (default ''), performed_by FK users, occurred_at, created_at. Indexes:
  (unit_id, occurred_at), (event_type, occurred_at), component_barcode. CHECK constraints:
  component columns only on COMPONENT_TRACKED rows; REVERSED rows need reverses_id and a reason
  of 5+ characters.
- audit_log: id, actor_id FK null (null = system), action, entity_type, entity_id, entity_repr,
  old_values jsonb, new_values jsonb, ip_address inet, created_at.
- All FKs ON DELETE RESTRICT. All times are UTC DateTime mapped to timestamptz.
- APPEND-ONLY for inventory_transactions and audit_log, enforced twice: AppendOnlyInterceptor
  (SaveChangesInterceptor) throws on Modified/Deleted entries, and a PostgreSQL function
  forbid_change() + BEFORE UPDATE OR DELETE FOR EACH ROW triggers (migrationBuilder.Sql).

DERIVED STATE (implemented only in Services/InventoryService.cs; never stored)
- effective rows of a unit = rows that are not REVERSED and are not referenced by a REVERSED row,
  ordered by (occurred_at, id).
- GetUnitStatusAsync(barcode) -> UnitStatus: NotFound | Registered | InStock(point, since) |
  Delivered(point, at), decided by the last effective RECEIVED/DELIVERED row.
- attached components = effective COMPONENT_TRACKED rows of the unit.
- a component barcode is taken if ANY unit has an effective COMPONENT_TRACKED row for it.
- current stock count = number of units whose status is InStock.

BUSINESS RULES
R1  One role per user; every API action is authorized server-side via policies.
R2  The first scan of an unknown barcode (track or receive) registers a Unit; the user must choose
    the product. Delivering an unknown barcode is an error.
R3  Track: the unit must not be Delivered; each component barcode must not be taken (taken by this
    unit -> duplicate-scan warning; by another unit -> error naming that unit's barcode);
    component barcode != product barcode; a TRACK point is required.
R4  Receive: only when status is Registered. InStock -> duplicate-scan warning (point, time,
    user). Delivered -> error. A RECEIVE point is required.
R5  Deliver: only when InStock; a DELIVERY point is required; the unit then leaves the stock count.
R6  History rows are never edited or deleted (enforced in the app AND in the DB).
R7  Reversal: ADMIN only; reason >= 5 chars; creates a REVERSED row with reverses_id=<row>; a row
    can be reversed at most once; REVERSED rows cannot be reversed.
R8  Reversing a RECEIVED row is allowed only if the unit is not Delivered -> status Registered.
R9  Reversing a DELIVERED row (only the latest effective one) -> the unit is InStock again at the
    receive point of its last effective RECEIVED row; the reversal row's point = that point.
R10 Reversing a COMPONENT_TRACKED row detaches the component; its barcode can be tracked again.
R11 Master data that is in use is soft-deleted (hidden from dropdowns, shown in history,
    restorable); unused rows may be hard-deleted after confirmation.
R12 Users are activated/deactivated, never deleted. Deactivation calls UpdateSecurityStampAsync
    and SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero, so the user's next API
    call returns 401 and the React app returns to /login with "Your session has ended";
    ImsSignInManager.CanSignInAsync refuses inactive users. Nobody can deactivate themselves or
    demote/deactivate the last active ADMIN.
R13 Every master-data and user change and every login/logout/failed login goes to audit_log with
    old and new values (never PasswordHash, SecurityStamp or ConcurrencyStamp).
Barcodes: trim + uppercase, must match ^[A-Z0-9-]{6,32}$ (server constant Barcode.Pattern and
client lib/barcode.ts must be identical; a test on each side pins it). Inventory writes run in a
transaction that first locks the unit row (SELECT ... FOR UPDATE).

API CONTRACT (all under /api; JSON camelCase; times ISO 8601 UTC)
  auth:    GET csrf | POST login {userName,password} | POST logout | POST register | GET me
           -> {id,userName,displayName,role,employeeNo,capabilities[]} | POST change-password
  config:  GET config -> {timeZone}
  units:   GET units/lookup?barcode=
  ops:     POST ops/track | POST ops/receive | POST ops/deliver
  history: GET history | GET history/{id}/reverse-preview | POST history/{id}/reverse {reason}
  reports: GET reports/stock | GET reports/summary?from&to | GET reports/barcode?barcode=
  admin:   products | components | points: GET list, GET {id}, POST, PUT {id}, DELETE {id},
           POST {id}/restore ; users: GET list, GET {id}, POST, PUT {id},
           POST {id}/toggle-active, POST {id}/reset-password
  audit:   GET audit | GET audit/{id}
  dashboard: GET dashboard
  Lists: query q, page, pageSize (default 20), sort, plus filters -> PagedResult {items, page,
  pageSize, totalCount}. Export: the same GET with format=csv|xlsx returns a file (same filters,
  no paging).
  Errors: 400 ValidationProblemDetails (field errors) | 401 not signed in | 403 forbidden |
  404 | 409 duplicate scan: ProblemDetails type "duplicate-scan" with {when, where, by} |
  422 business rule: ProblemDetails with "code" and optional field errors.

REACT ROUTES (each wrapped in RequireAuth + RequireCapability where needed)
  /login  /register  /change-password  /forbidden
  /                      dashboard (ops roles are sent to their scan page after login)
  /ops/track  /ops/receive  /ops/deliver
  /history  /history/:id/reverse
  /reports/stock  /reports/summary  /reports/barcode
  /manage/products  /manage/components  /manage/points  /manage/users  (list + create/edit forms)
  /audit
  List state (q, page, filters) lives in the URL search params so Back and shared links work.

CONVENTIONS
- All inventory rules live in InventoryService. Controllers stay thin: validate, call the service,
  map results to DTOs. DomainException -> 422, DuplicateScanException -> 409 (one place:
  Infrastructure ProblemDetails mapping).
- Client: every request goes through api/client.ts; server data only via TanStack Query hooks
  (queries keyed by area + params; mutations invalidate the affected keys). Forms use
  react-hook-form + zod; server field errors are copied into the form with setError.
- Server-side validation is the authority; zod schemas mirror it for fast feedback.
- Times: UTC on the wire; format in the browser with Intl.DateTimeFormat and the configured zone.
- Every rule and every policy gets an API test; client logic (barcode, duplicate-scan batch,
  guards, error mapping) gets Vitest tests. Before every commit: dotnet build, dotnet test,
  npm run build --prefix client, npm test --prefix client, all green.
- Record every judgment call in DECISIONS.md (date, decision, reason). Keep a "Carry-over" section;
  clear it first each day.
- Append a dated section to docs/LEARNING_NOTES.md every day: the .NET and React concepts used,
  each in 2-3 sentences with the file path where it appears. Plain English for a beginner.
- Conventional Commits (feat:, fix:, test:, docs:, chore:, refactor:). Work on main; tag each day.

SEED DATA (dotnet run --project src/Ims.Api -- seed ; idempotent; exits after seeding.
`-- seed --demo-data` adds demo data from Day 5 on)
Users (created through UserManager so passwords are hashed; demo passwords only):
  admin     ADMIN              employee_no 10001  Admin@12345
  tracker   COMPONENT_TRACKER  employee_no 66798  Demo@12345
  receiver  RECEIVE_USER       employee_no 58877  Demo@12345
  deliverer DELIVERY_USER      employee_no 54671  Demo@12345
  viewer    REPORT_VIEWER      employee_no 20001  Demo@12345
Master data: Products Refrigerator, Freezer, Air Conditioner. Components Compressor, Condenser,
  Evaporator, Thermostat. Points: "Refrigerator Component Track Point 1" (TRACK),
  "Refrigerator Receive Point 1", "Refrigerator Receive Point 2" (RECEIVE),
  "Refrigerator Delivery Point 1", "Refrigerator Delivery Point 2" (DELIVERY).
Sample record from the brief, seeded EXACTLY, in this order (times read in App:TimeZone):
  1) 2026-03-27 10:00:00 COMPONENT_TRACKED unit 2440701AK2057 (product Refrigerator),
     component Compressor, component_barcode 94A600321852, point Refrigerator Component Track
     Point 1, by employee 66798 (tracker). This first scan registers the unit.
  2) 2026-03-28 10:15:00 RECEIVED, point Refrigerator Receive Point 1, by 58877 (receiver)
  3) 2026-03-30 11:30:00 DELIVERED, point Refrigerator Delivery Point 1, by 54671 (deliverer)
  Afterwards GetUnitStatusAsync("2440701AK2057") is Delivered and its history shows 3 rows.

DATABASE
  Local PostgreSQL 17 on localhost:5432. Database `ims` owned by role `ims_user` (LOGIN CREATEDB).
  Tests use `ims_test` (dropped and re-created by the test fixture). Schema changes ONLY through
  EF Core migrations. Reset = dotnet ef database drop --force, database update, seed.
  SQL deliverable = `dotnet ef migrations script` + pg_dump. Never read, print or echo the
  connection string or its password (do not run `dotnet user-secrets list`).

RUN
  dotnet build ; npm ci --prefix client
  dotnet ef database update --project src/Ims.Api ; dotnet run --project src/Ims.Api -- seed
  Dev:  dotnet run --project src/Ims.Api --launch-profile http   (API :5080)
        npm run dev --prefix client                              (open http://localhost:5173)
  Demo: npm run build --prefix client, then dotnet run --project src/Ims.Api --launch-profile http
        (open http://localhost:5080)
  Tests: dotnet test ; npm test --prefix client

DAY PLAN (for orientation)
  Day 1 skeleton end-to-end | Day 2 admin panel, users, account security | Day 3 operations
  (track/receive/deliver) | Day 4 reversals, reports, export | Day 5 audit log, dashboard, demo
  data, final diagrams + SQL | Day 6 QA, test sheet, user guide, demo, submission

GUARDRAILS (always)
- Do not break earlier features; run all four build/test commands before committing.
- If something is ambiguous, choose the simplest sensible option, record it in DECISIONS.md,
  and continue. Do not stop to ask unless you are truly blocked.
- Never edit or delete inventory_transactions or audit_log rows, not even in migrations or seeds.
- Never put secrets in files that git tracks. Never ask the intern to paste a password into chat.
- Do not add Next.js, Redux/Zustand, a React UI kit, Docker, MediatR, AutoMapper, generic
  repositories, JWT/localStorage auth or a different database.
=== END PROJECT CONTEXT BLOCK ===

=== DAY 6 OF 6: QA, TEST-CASE SHEET, USER GUIDE, DEMO, SUBMISSION ===

1. ROLE
You are a QA engineer and technical writer (with a full-stack developer's ability to fix
defects). Your job is to prove the system works for every role and every rule, document it for a
first-time user, and prepare a 10-minute demo. No new features today.

2. CONTEXT & CURRENT STATE
Days 1–5 are done (tags day-1..day-5): feature complete. React client + ASP.NET Core API with
Identity cookie + policies for 5 roles; admin panel; Track/Receive/Deliver with duplicate-scan
warnings; admin reversals; stock, summary (+chart) and barcode reports; CSV/Excel export; audit
log; dashboard; seed (--demo-data); final Mermaid diagrams; docs/db/schema.sql + data dump; xUnit
and Vitest tests.
MUST NOT REGRESS: anything. Bug fixes only, each with a test.
Starting-state check:
    git checkout main && git pull ; git tag --list "day-*"  -> day-1..day-5
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Clear any Carry-over items in DECISIONS.md first; they are blockers for submission.

3. TODAY'S OBJECTIVE
Produce every remaining PDF deliverable: an executed test-case sheet, a user guide with
screenshots of every module, the final README, diagram images, a demo script + slides, and a
submission checklist, then tag v1.0.0. Advances: all Deliverables; verifies every requirement.

4. ORDERED TASK LIST
 1. Fresh-clone check: clone the repo into a temporary folder and follow README setup EXACTLY
    (user-secrets with Database=ims_fresh so the main database is untouched, npm ci, database
    update, seed, both run modes, tests). Fix any step that fails or is missing.
 2. Playwright (Node, dev dependency in client: @playwright/test; `npx playwright install
    chromium`), scripts in client/e2e/:
    - screenshots.spec.ts: against demo mode (http://localhost:5080, seeded with --demo-data),
      logs in as each role and saves full-page PNGs of every module to docs/user-guide/img/ with
      stable names (01-login.png, 02-dashboard-admin.png, 03-track.png, ..., incl. the Forbidden
      page, a duplicate-scan warning, a reversal confirm, the summary chart, export buttons, the
      audit detail)
    - smoke.spec.ts: one end-to-end run of the sample flow (track -> receive -> deliver ->
      reverse) through the real UI
    - render-diagrams.ts: renders the two Mermaid files (Mermaid from a pinned CDN URL) to
      docs/diagrams/process-flow.png/.pdf and er-diagram.png/.pdf; if offline, record a manual
      mermaid.live export in DECISIONS.md
 3. tools/Ims.DocTools (console, ClosedXML): docs/testing/test-cases.csv -> test-cases.xlsx
    (coloured Pass/Fail, frozen header, filters).
 4. Test-case sheet: docs/testing/test-cases.csv with columns ID, Area, Requirement, Role,
    Preconditions, Steps, Expected result, Actual result, Status (Pass/Fail), Automated test (test
    name or "manual"); at least 45 cases: the full policy matrix (every role x every capability,
    API and UI), rules R1–R13, each additional requirement, the sample record end-to-end, and
    negative cases (bad barcode format, archived master data, invalid date range, short reversal
    reason, missing antiforgery header). EXECUTE every case: automated via dotnet test, npm test
    and the Playwright smoke test; manual ones in the running app. Fill in actual results
    honestly. Each failure: fix with a regression test (separate `fix:` commits), re-run, update.
    Then build the .xlsx.
 5. docs/user-guide/USER_GUIDE.md: intro, sign in / register / change password, then one section
    per role with step-by-step screenshots; every module covered; duplicate-scan and error
    messages explained; FAQ/troubleshooting (forgot password -> ask admin, "account not active",
    "session has ended", what Forbidden means); glossary (unit, component, receive point,
    delivery point, reversal).
 6. README.md final: overview + feature list mapped to the brief, architecture diagram (React ->
    API -> PostgreSQL), stack + rationale (≤5 lines), prerequisites (.NET 10 SDK, dotnet-ef, Node
    LTS, PostgreSQL 17, Git), database setup (ims_user, ims, user-secrets command), dev and demo
    run modes, seed (with/without demo data), reset, tests (dotnet test, npm test, Playwright),
    default admin login + one login per role, project structure, docs index, known limitations
    (demo scale, local PostgreSQL, no camera scanning), link to DECISIONS.md.
 7. Demo kit:
    - docs/demo/DEMO_SCRIPT.md: a timed 10-minute script in DEMO MODE (one process on :5080):
      0:00 intro + stack (slides); 1:00 roles + Forbidden page + API 403; 2:00 admin: master
      data, soft delete, user activation + forced logout; 3:30 sample record barcode history
      (3 rows); 4:30 a new unit live: track (+ duplicate-scan warning) -> receive -> deliver with 3
      accounts; 6:30 admin reversal + barcode history showing it; 7:30 reports: stock, summary
      chart, Excel export; 8:30 dashboard + audit log; 9:15 diagrams + test sheet; 9:45 Q&A.
      Exact barcodes and a pre-demo checklist (PostgreSQL running, reset DB, seed --demo-data,
      `npm run build`, two browser windows, accounts logged in).
    - docs/demo/slides.md: 7–9 Marp-compatible slides (title, problem, roles, architecture, flow
      diagram, ER diagram, key rules, screenshots, testing results, what I learned).
 8. docs/SUBMISSION.md: checklist mapping each PDF deliverable to its file path and status, plus
    the repo URL and the tag to review (v1.0.0).
 9. Final pass: `dotnet build -warnaserror` (fix or justify warnings), `npm run build` with no
    TypeScript errors, all tests green, no TODO/console.log/debug output left, no secrets
    committed (`git grep -i password` shows only docs/demo text), .gitignore covers bin/ obj/
    node_modules/ wwwroot build and Playwright output. LEARNING_NOTES.md Day 6 (integration
    tests with WebApplicationFactory, component tests with Vitest, end-to-end tests with
    Playwright, what to learn next).

5. ACCEPTANCE CRITERIA
- [ ] A fresh clone set up from README alone runs in both modes and passes all tests
- [ ] docs/testing/test-cases.xlsx has 45+ executed cases with actual results; all Pass, or
      failures documented with a reason and owner
- [ ] docs/user-guide/USER_GUIDE.md has a screenshot of every module for every role
- [ ] docs/diagrams/ contains process-flow and ER as Mermaid source + PNG/PDF
- [ ] docs/db/ contains the schema script and the dump with sample data
- [ ] README has setup, stack rationale, default admin and one login per role
- [ ] DEMO_SCRIPT.md is rehearsable in 10 minutes; slides.md exists
- [ ] docs/SUBMISSION.md maps every PDF deliverable to a path
- [ ] Git history shows daily tags day-1..day-6 and v1.0.0

6. VERIFICATION
    dotnet build -warnaserror && dotnet test                            -> green
    npm test --prefix client && npm run build --prefix client           -> green
    dotnet run --project tools/Ims.DocTools -- test-sheet               -> test-cases.xlsx written
    (terminal 1) dotnet run --project src/Ims.Api -- seed --demo-data
                 dotnet run --project src/Ims.Api --launch-profile http
    (terminal 2) npx --prefix client playwright test                    -> smoke + screenshots
                 node client/e2e/render-diagrams.ts (or the documented npm script)
    git log --oneline --decorate                                        -> readable history, all tags
Rehearse the demo script once with a timer.

7. DEMO NOTE
This is demo day: run DEMO_SCRIPT.md end-to-end for the mentor (10 minutes), finishing on the
test-case sheet and the submission checklist.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "docs: test-case sheet, user guide with screenshots, diagrams, demo script and final README"
    git push origin main
    git tag -a day-6 -m "Day 6: QA, docs, demo"
    git tag -a v1.0.0 -m "Submission: Inventory Management System v1.0.0"
    git push origin day-6 v1.0.0
(Defect fixes earlier in the day are separate commits such as
 "fix(inventory): block delivery to an archived delivery point".)

9. GUARDRAILS
- Do not break prior features: no new features; every fix comes with a regression test.
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Report test results honestly; a failing case is documented, never marked Pass.
- Screenshots use seeded demo accounts only; no real personal data in docs.
