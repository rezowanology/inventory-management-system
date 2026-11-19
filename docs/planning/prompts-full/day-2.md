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

=== DAY 2 OF 6: ADMIN PANEL, USER MANAGEMENT, ACCOUNT SECURITY ===

1. ROLE
You are a senior full-stack engineer focused on admin tooling and account security with ASP.NET
Core Identity and React forms. Explain Identity, controller and React form idioms briefly for a
learner.

2. CONTEXT & CURRENT STATE
Day 1 is done and tagged `day-1`: Ims.Api (controllers, Identity cookie + antiforgery, policies
from Capabilities.cs, full schema with append-only triggers, seed with the sample record, real
GET /api/history and /api/dashboard, every other endpoint a 501 stub) and the React client
(Vite, router, AuthProvider + guards, layout from me.capabilities, real /history and dashboard,
every other route stubbed), API and Vitest tests, README, DECISIONS.md, LEARNING_NOTES.md, draft
diagrams.
MUST NOT REGRESS: policy matrix tests, append-only guarantees, the sample record, cookie login,
role-based landing pages, demo mode build.
Starting-state check (do this first):
    git checkout main && git pull ; git tag --list "day-*"  -> day-1
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Then read DECISIONS.md and finish any Carry-over items before starting new work.

3. TODAY'S OBJECTIVE
Replace the admin stubs with a working admin panel for master data and users (API + React),
including soft delete, activation, password change and forced logout, on top of a shared list
contract. Advances: Core 1 (complete), Core 2; Additional 1 (admin lists), 4, 5 (admin forms), 7.

4. ORDERED TASK LIST
 1. Shared list contract. Server: ListQuery (q, page, pageSize = 20, sort; bound with
    [FromQuery]) and PagedResult<T>.CreateAsync(IQueryable, ...). Client: components DataTable
    (columns config), ListToolbar (search box + filter selects + Export buttons slot), Pagination
    ("Showing X–Y of Z"), and a useListParams hook that keeps q/page/filters in the URL search
    params. Export buttons render but the API returns 501 until Day 4.
 2. Master data API (Products, Components, Points controllers, masterdata.manage): list (search;
    filter active/deleted/all; Points also by type), get, create, update, delete, restore.
    Request DTOs with DataAnnotations ([Required], [StringLength(100, MinimumLength = 2)]); trim;
    duplicate name -> 422 with a field error (service check first; the DB index is the safety net).
    Delete: referenced by units or history -> soft delete and return {archived: true}; otherwise
    hard delete. A GET .../options endpoint (or ?activeOnly=true) feeds every dropdown with
    non-deleted rows only.
 3. Master data UI (/manage/products, /manage/components, /manage/points): list with search,
    filters and paging; create/edit form (react-hook-form + zod mirroring the server rules; server
    field errors copied with setError); delete with ConfirmDialog, then "In use: archived instead
    of deleted" or "Deleted"; Restore button on archived rows. Invalidate the list query after
    each mutation.
 4. Users API (UsersController, users.manage) via UserAdminService using UserManager: list
    (search username/name/employee_no; filter role, active, "pending registration" = inactive +
    never logged in), get, create (username, names, employee_no unique, one role, initial
    password with Identity validators), update (names, employee_no, role), toggle-active (block
    self and the last active ADMIN with 422 + clear messages), reset-password (admin sets a new
    one; the old one is never returned).
 5. Forced logout: deactivation calls UpdateSecurityStampAsync; with ValidationInterval = Zero the
    user's next API call returns 401; the React client clears the user and shows /login with
    "Your session has ended. Please sign in again." Inactive login attempts show "This account is
    not active".
 6. Password change: POST /api/auth/change-password (old, new x2) with ChangePasswordAsync then
    RefreshSignInAsync; React /change-password page for every role, with success message.
 7. Users UI (/manage/users): list, create/edit forms with a single role select, activate/
    deactivate with ConfirmDialog, reset-password dialog, a "Pending registration" filter chip.
 8. Tests (API): master data CRUD + case-insensitive uniqueness; soft delete when referenced vs hard
    delete when not; dropdown options exclude archived rows; user create/edit/role change;
    self-deactivate and last-admin blocked; a deactivated user's existing cookie gets 401 on the
    next call (real cookie flow); password change works and the old password stops working; all
    admin endpoints 403 for non-admin roles. Vitest: useListParams round-trips URL params;
    server field errors land on the right form fields.
 9. README "Admin panel"; DECISIONS.md (soft-delete rules, last-admin protection, zero-interval
    stamp validation); LEARNING_NOTES.md Day 2 (DTOs vs entities, model binding + DataAnnotations,
    UserManager and security stamps, ProblemDetails; React: controlled forms with react-hook-form,
    zod schemas, useMutation + invalidateQueries, URL search params as state).

5. ACCEPTANCE CRITERIA
- [ ] Admin can create, edit, search, filter and page products, components and points
- [ ] Deleting "Refrigerator" (used by the sample unit) archives it; it disappears from dropdowns
      but /history still shows it; Restore brings it back
- [ ] Deleting a brand-new unused product removes it after a confirm dialog
- [ ] Duplicate names (e.g. " compressor ") show a clear error on the name field
- [ ] Admin can create a user with any one role and change the role later
- [ ] Deactivating `receiver` while receiver is logged in elsewhere sends them to the login page
      with "Your session has ended" on their next action; they cannot log in until reactivated
- [ ] A self-registered user appears under "pending registration" and can log in after activation
- [ ] Admin cannot deactivate self or the last active admin
- [ ] Every role can change their own password
- [ ] Back/forward and copied URLs keep list filters and page
- [ ] All Day 1 tests and today's new tests pass

6. VERIFICATION
    dotnet build && dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed
    dotnet test && npm test --prefix client && npm run build --prefix client   -> green
    run in dev mode (two terminals) and open http://localhost:5173
Manual: open two browsers (normal + private window). Log in as admin in one and receiver in the
other. Deactivate receiver as admin, then click anything in receiver's window -> login page with
the session-ended message.

7. DEMO NOTE
Live forced logout: deactivate `receiver` in one window and show the other window being signed
out. Then archive "Refrigerator" and show it gone from the dropdown but still in history.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "feat(admin): master data CRUD with soft delete, user management, password change and forced logout"
    git push origin main
    git tag -a day-2 -m "Day 2: admin panel and account security"
    git push origin day-2

9. GUARDRAILS
- Do not break prior features: the policy matrix tests must stay green, and the Day 1 stubs for
  ops and reports must stay reachable and guarded.
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Never hard-delete users. Never touch inventory_transactions rows.
- Use the shared list contract and components; no per-page paging code.
