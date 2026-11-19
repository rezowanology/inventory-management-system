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

=== DAY 3 OF 6: OPERATIONS: COMPONENT TRACKING, RECEIVE, DELIVERY ===

1. ROLE
You are a senior full-stack engineer who owns the domain rules. Correctness of the inventory rules
matters more than visuals today. Show the intern how a service layer, transactions and typed
errors keep rules in one place, and how the React scan screens stay fast for a scanner user.

2. CONTEXT & CURRENT STATE
Days 1–2 are done (tags day-1, day-2): schema with append-only history, Identity cookie +
antiforgery + policies, React shell with guards, seed with the sample record, real history and
dashboard counts, a full admin panel (master data with soft delete, users with roles/activation/
forced logout, password change), and the shared list contract (ListQuery/PagedResult, DataTable,
ListToolbar, Pagination, useListParams). The ops endpoints, /api/units/lookup and the three
/ops pages are still stubs. InventoryService has the read methods; TrackAsync, ReceiveAsync,
DeliverAsync are stubs.
MUST NOT REGRESS: policy tests, append-only tests, seed tests, admin panel tests.
Starting-state check:
    git checkout main && git pull ; git tag --list "day-*"  -> day-1, day-2
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Clear any Carry-over items in DECISIONS.md first.

3. TODAY'S OBJECTIVE
Implement the three scan operations end-to-end with every business rule enforced in
InventoryService, scanner-friendly React pages, client + server validation with a duplicate-scan
warning, a database safety-net trigger, and a filterable history page.
Advances: Core 3, 4, 5, 6; Additional 1 (history), 5.

4. ORDERED TASK LIST
 1. InventoryService: implement
    - GetOrRegisterUnitAsync(barcode, productId?, user)
    - TrackAsync(barcode, productId?, trackPointId, IReadOnlyList<(componentId, componentBarcode)>,
      user): R2, R3; validates the whole batch first, then writes all rows or none
    - ReceiveAsync(barcode, productId?, receivePointId, user): R2, R4
    - DeliverAsync(barcode, deliveryPointId, user): R5
    Each: one transaction (BeginTransactionAsync) that first locks the unit row with
    FromSql("SELECT * FROM units WHERE barcode = {0} FOR UPDATE") when it exists; normalized
    barcode; point-type checks; active master data only; occurred_at = UtcNow via Clock.
    DomainException(code, message, field?) for hard errors; DuplicateScanException(when, where,
    by) for repeats; specific messages such as "Component 94A600321852 is already attached to
    unit 2440701AK2057".
 2. Migration "TransactionChecks": PL/pgSQL check_transaction() + BEFORE INSERT trigger on
    inventory_transactions rejecting a wrong point type, a component barcode already attached
    (ignoring reversed rows), and reversing a REVERSED row. IMPORTANT PL/pgSQL detail: compute
    the expected point type into a variable first; a CASE inside an IF condition ends the
    condition at its first THEN.
 3. API: GET /api/units/lookup?barcode= (ops.lookup) -> {barcode, exists, product, status,
    currentPoint, since, components[], recentEvents[5]}; POST /api/ops/track | receive | deliver
    (matching policies) with request DTOs + DataAnnotations; responses return the updated unit
    status so the UI can refresh without another call. 409/422 come from the shared
    ProblemDetails mapping.
 4. React ScanPanel component + useUnitLookup hook: barcode input with autoFocus; Enter (a USB
    scanner sends Enter) triggers the lookup query; uppercase on input; the pattern mirrors
    lib/barcode.ts; a unit card with StatusBadge (Registered grey, InStock green, Delivered blue,
    NotFound amber). After a successful save: success alert, form reset, focus back on the
    barcode input ("scan next"). The last used point is remembered (sessionStorage, wrapped in
    try/catch).
 5. /ops/track: scan the product barcode -> if unknown, a product select appears (required);
    choose the TRACK point; add component rows (component select + component barcode input,
    useFieldArray). Client: a component barcode scanned twice in this batch, or equal to the
    product barcode, shows a yellow inline "Duplicate scan" warning and is not added. Submit ->
    server validates the whole batch; field errors map back onto the matching row.
 6. /ops/receive: scan -> unit card; if unknown, a product select; choose the RECEIVE point ->
    save. 409 -> a warning alert with point/time/user and nothing saved; 422 -> error alert.
 7. /ops/deliver: scan -> unit card; Save enabled only when InStock (the server re-checks);
    choose the DELIVERY point -> save.
 8. /history upgrade (API + UI) with the list contract: columns time (local), barcode, product,
    event badge, point, component name + barcode, user (employee_no + name), reason. Filters:
    event type, point, product, user, date from/to; search across product or component barcode;
    paging. Leave a placeholder for the "reversed" badge (logic tomorrow).
 9. Tests: API/service tests for every rule (first scan registers; product required for a new
    unit; receive InStock -> 409; receive Delivered -> 422; deliver Registered -> 422; deliver
    InStock -> Delivered and stock count decreases; component taken by another unit -> 422;
    component already on this unit -> 409; batch atomicity; wrong point type rejected by the
    service AND by the trigger via raw SQL; archived master data rejected; each ops endpoint 403
    for wrong roles; lookup JSON shape). Vitest: batch duplicate detection; ScanPanel uppercases
    and submits on Enter; 409 renders the warning alert.
10. README "Operations"; DECISIONS.md (warning vs error, row locking); LEARNING_NOTES.md Day 3
    (service layer + DI lifetimes, async/await with EF Core, LINQ, transactions, custom
    exceptions to ProblemDetails; React: custom hooks, useFieldArray, refs and focus, query
    invalidation after mutations).

5. ACCEPTANCE CRITERIA
- [ ] As `tracker`: scan a NEW barcode (e.g. TESTUNIT0001), choose Refrigerator and the track
      point, attach two components -> two history rows appear
- [ ] Scanning 94A600321852 onto TESTUNIT0001 fails: "already attached to unit 2440701AK2057"
- [ ] Scanning the same component twice in one batch shows the client-side duplicate warning
- [ ] As `receiver`: receive TESTUNIT0001 at Receive Point 1 -> InStock; receiving again shows
      the duplicate-scan warning with point/time/user and saves nothing
- [ ] As `deliverer`: deliver TESTUNIT0001 -> Delivered; the dashboard stock count goes back down
- [ ] Delivering 2440701AK2057 (already delivered) or an unknown barcode -> a clear error
- [ ] /history (admin/viewer) filters by event, point, user and date range, searches by barcode,
      and pages
- [ ] Every rule R2–R5 has a passing test; all earlier tests still pass

6. VERIFICATION
    dotnet build && dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed
    dotnet test --filter "FullyQualifiedName~Inventory"    -> green
    dotnet test && npm test --prefix client && npm run build --prefix client   -> green
    run in dev mode and open http://localhost:5173
Manual: run the acceptance flow with the three ops users in the order track -> receive ->
deliver, then check /history as viewer.

7. DEMO NOTE
Run a brand-new unit through Track -> Receive -> Deliver live with the three role accounts, and
trigger one duplicate-scan warning on purpose.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "feat(inventory): component tracking, receive and delivery with validation and duplicate-scan warnings"
    git push origin main
    git tag -a day-3 -m "Day 3: core operations"
    git push origin day-3

9. GUARDRAILS
- Do not break prior features (admin panel, policies, seed).
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Never add a status column or edit history rows: status is always derived in InventoryService.
- Business rules live only in InventoryService (plus the DB safety net); React only mirrors them.
