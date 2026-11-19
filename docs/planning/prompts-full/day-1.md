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

=== DAY 1 OF 6: SKELETON END-TO-END ===

1. ROLE
You are a senior full-stack engineer (ASP.NET Core + React/TypeScript) and tech lead setting up a
project that a junior intern, who is learning C# and React this week, will extend for five more
days. Favour clarity and framework conventions over cleverness, and explain idioms briefly in
comments and LEARNING_NOTES.md.

2. CONTEXT & CURRENT STATE
- The working folder is an empty clone of the GitHub repo (only .git), or an empty folder.
- Installed: .NET SDK 10.0.404, dotnet-ef 10, Node.js 26 LTS + npm 11, PostgreSQL 17 (running).
  The database `ims` and the role `ims_user` (LOGIN CREATEDB) exist. The intern knows the password
  and will store it as a user secret; never ask for it in chat, never print it.
- Nothing can regress yet, but everything you build today is the base every later day relies on.
  Build the WHOLE structure today: every entity, every role, every endpoint, every React route.

3. TODAY'S OBJECTIVE
Ship a running React + ASP.NET Core app with the complete schema (EF Core migrations on
PostgreSQL), Identity cookie login with antiforgery and server-side policy authorization, a
role-aware React layout where every route is reachable (stubbed where needed), the exact sample
record seeded, tests on both sides, and a first README. Advances: Core 1 (auth), Core 6
(append-only history + derived status, first version), and the skeleton of all others.
Deliverables: draft flow + ER diagrams.

4. ORDERED TASK LIST
 1. Backend solution: `dotnet new gitignore` (then add node_modules/, client/dist/,
    src/Ims.Api/wwwroot/, playwright-report/, test-results/); global.json; `dotnet new sln -n
    Ims`; `dotnet new webapi --use-controllers -n Ims.Api -o src/Ims.Api`; `dotnet new xunit -n
    Ims.Tests -o tests/Ims.Tests`; add both to the solution; tests reference Ims.Api.
    Directory.Build.props: Nullable + ImplicitUsings. launchSettings "http" profile ->
    http://localhost:5080. Remove the template's sample controller.
 2. NuGet (pin the exact versions you resolve; list them in README): Npgsql.EntityFrameworkCore.
    PostgreSQL 10.0.x, EFCore.NamingConventions 10.0.x, Microsoft.AspNetCore.Identity.
    EntityFrameworkCore 10.0.x, Microsoft.EntityFrameworkCore.Design 10.0.x, ClosedXML 0.105.x;
    tests: Microsoft.AspNetCore.Mvc.Testing 10.0.x.
 3. Secrets: `dotnet user-secrets init --project src/Ims.Api`. Program.cs reads
    ConnectionStrings:Ims; if missing, throw an InvalidOperationException whose message shows the
    exact `dotnet user-secrets set ...` command (placeholder password). Then STOP and ask the
    intern to run it themselves; continue when they say it is done. appsettings.json holds only
    App:TimeZone and logging.
 4. Domain + data exactly as in DATA MODEL: one IEntityTypeConfiguration per entity, enums as
    strings, ImsDbContext : IdentityUserContext<ApplicationUser, long> with
    UseSnakeCaseNamingConvention() and the users table named "users". Migration "InitialCreate";
    migration "AppendOnlyAndIndexes" (migrationBuilder.Sql: forbid_change() + triggers on
    inventory_transactions and audit_log, partial unique lower(name) indexes; Down() reverses).
    Register AppendOnlyInterceptor with AddInterceptors.
 5. Auth on the server: Roles.cs, Capabilities.cs, one policy per capability + FallbackPolicy;
    ImsClaimsPrincipalFactory; ImsSignInManager (CanSignInAsync refuses inactive users);
    AddIdentityCore + AddEntityFrameworkStores + AddSignInManager + AddClaimsPrincipalFactory;
    AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies(); cookie events
    return 401/403 for /api paths; SecurityStampValidatorOptions.ValidationInterval = Zero;
    antiforgery (header X-XSRF-TOKEN, AutoValidateAntiforgeryToken global filter, GET
    /api/auth/csrf). AuthController: login (inactive -> 403 ProblemDetails "account inactive"),
    logout, register (creates an INACTIVE REPORT_VIEWER), me (with capabilities), and
    change-password returning 501 until Day 2. ConfigController returns the time zone.
    ProblemDetails: AddProblemDetails + the exception mapping in Infrastructure.
 6. Stub every other endpoint in the API CONTRACT: real [Authorize(Policy = ...)] attribute,
    returns 501 "Built on Day N". Real today: GET /api/history (all rows, newest first, simple
    paging) and GET /api/dashboard (total units + current stock count).
 7. InventoryService: implement NormalizeBarcode, GetEffectiveRowsAsync, GetUnitStatusAsync,
    GetAttachedComponentsAsync; stubs (NotImplementedException + XML docs) for TrackAsync,
    ReceiveAsync, DeliverAsync, ReverseAsync. DomainException and DuplicateScanException.
 8. DbSeeder + `seed` argument (seeds and exits; expects `dotnet ef database update` first):
    idempotent; users via UserManager; master data; the 3-row sample record EXACTLY (times read
    in App:TimeZone, stored as UTC). Static files + MapFallbackToFile("index.html") for the
    built client.
 9. Frontend: `npm create vite@latest client -- --template react-ts`; add react-router,
    @tanstack/react-query, react-hook-form, zod, @hookform/resolvers, bootstrap, chart.js,
    react-chartjs-2; dev: vitest, @testing-library/react, @testing-library/jest-dom, jsdom.
    vite.config.ts: proxy /api -> http://localhost:5080, build.outDir ../src/Ims.Api/wwwroot,
    emptyOutDir; test environment jsdom. package.json scripts: dev, build, test (vitest run).
10. Client foundation: api/client.ts (fetch with credentials "same-origin", JSON, X-XSRF-TOKEN
    from the cookie on unsafe methods, ProblemDetails -> ApiError {status, title, detail, code,
    fieldErrors}, 401 -> clear the cached user and go to /login); AuthProvider loading /api/auth/me
    (re-fetch csrf after login/logout); RequireAuth and RequireCapability (-> /forbidden);
    lib/barcode.ts; lib/dates.ts.
11. Client shell: Layout with a Bootstrap navbar grouped as Operations (Track, Receive, Deliver),
    Records (History), Reports (Stock, Summary, Barcode history), Admin (Products, Components,
    Points, Users, Audit log), plus a user menu (name, role badge, employee_no, Change password,
    Logout), showing only items in me.capabilities. Pages: Login (Development-only "Demo accounts"
    table), Register, Forbidden, and a stub page for every route in REACT ROUTES ("Built on Day
    N"). Real today: /history (table of rows with local times) and / (welcome card with the role's
    allowed actions + total units + stock count). After login, ops roles go to their scan page.
12. Tests: ImsApiFactory : WebApplicationFactory<Program> using ims_test (swap the database name
    with NpgsqlConnectionStringBuilder; drop/migrate/seed once per run) and a TestAuthHandler so
    a test can act as any role via a header. Add `public partial class Program { }`.
    - Policy matrix: [Theory] over 5 roles x every endpoint -> 403 or not-403 as per the matrix;
      anonymous -> 401.
    - Real cookie login: GET csrf, POST login, GET me returns capabilities; inactive user -> 403.
    - Append-only: EF change throws; raw SQL UPDATE fails (trigger).
    - Seed twice -> exactly 3 rows for 2440701AK2057, status Delivered.
    - Vitest: barcode normalize/regex; RequireCapability redirects when the capability is missing;
      client.ts maps a ProblemDetails body to ApiError.
13. Docs: docs/diagrams/process-flow.md and er-diagram.md (Mermaid DRAFTS); DECISIONS.md
    (A1–A15 + today's choices); docs/LEARNING_NOTES.md Day 1 (solution layout, Program.cs and DI,
    controllers and attributes, EF Core DbContext + migrations, Identity cookies + policies,
    user-secrets, Vite + React components, React Router, TanStack Query basics, the /api proxy);
    README v1 (what it is, stack + rationale, prerequisites, setup incl. user-secrets, seeded
    logins, dev and demo run modes, tests, reset, status "Day 1 of 6").

5. ACCEPTANCE CRITERIA
- [ ] `dotnet build` and `npm run build --prefix client` succeed; `dotnet ef database update`
      creates all snake_case tables in `ims`
- [ ] The connection string is only in user-secrets; `git grep -i password` finds no secret
- [ ] `seed` runs twice without duplicates; the sample record is present exactly once
- [ ] At http://localhost:5173 all 5 seeded users can log in; each sees only their menu items;
      logout works; refreshing the page keeps the session (cookie)
- [ ] Opening a forbidden route (viewer -> /ops/receive) shows the Forbidden page, AND calling the
      matching API directly returns 403
- [ ] Registration creates an inactive Report Viewer that cannot log in yet
- [ ] Every route in REACT ROUTES is reachable by the right role (stub or real page)
- [ ] /history shows the 3 sample rows with barcode, event, point, user (employee_no), local time
- [ ] Dashboard shows total units = 1 and stock count = 0
- [ ] Demo mode works: after `npm run build`, http://localhost:5080 serves the same app
- [ ] UPDATE/DELETE on history fails in EF and in raw SQL (tests); all tests green
- [ ] README, DECISIONS.md, LEARNING_NOTES.md and both diagram drafts exist

6. VERIFICATION
    dotnet build                                            -> 0 errors
    dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed                (run twice: nothing new the 2nd time)
    dotnet test                                             -> all passed
    npm ci --prefix client && npm test --prefix client      -> all passed
    npm run build --prefix client                           -> builds into src/Ims.Api/wwwroot
    dev:  dotnet run --project src/Ims.Api --launch-profile http   +   npm run dev --prefix client
          -> log in as each user at http://localhost:5173
    demo: dotnet run --project src/Ims.Api --launch-profile http   -> http://localhost:5080

7. DEMO NOTE
Log in as `viewer`, show that the Operations menu is absent, type /ops/receive into the address
bar and show the Forbidden page; then show the API itself answering 403. Log in as `admin` and
show the 3 seeded history rows.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git status          (no secrets, no bin/ obj/ node_modules/ or wwwroot build staged)
    git add -A
    git commit -m "feat: scaffold React + ASP.NET Core IMS with Identity cookies, policies, full schema, seed data and stubbed routes"
    git push -u origin main
    git tag -a day-1 -m "Day 1: running skeleton end-to-end"
    git push origin day-1

9. GUARDRAILS
- Do not break prior features (none yet; from now on every day builds on this).
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Keep stubs honest: API stubs carry the real policy; React stubs sit behind RequireCapability.
- Do not implement Day 2–6 features beyond stubs; depth comes later.
