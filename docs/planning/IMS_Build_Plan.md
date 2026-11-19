# Inventory Management System: 6-Day Build Plan and Prompt Pack (React + .NET 10 API + PostgreSQL)

Source brief: *Inventory Management System – Industrial Attachment Assignment* (Nov 3, 2026). This pack contains one copy-paste prompt per working day. The intern pastes the **Project Context Block (Section 3)** followed by **that day's prompt** into a fresh coding-agent session, lets the agent build, checks the acceptance list, and the day ends with commit, push and a `day-N` tag.

The intern is learning C#, .NET and React while building this, so every prompt asks for idiomatic, readable code and a short daily entry in `docs/LEARNING_NOTES.md`.

---

## 0. Scope Understanding & Assumptions

**PDF read status.** All text in the brief was extracted and read. One part could not be read: the embedded figure captioned "system flow – 4 steps and reports" is an image. The flow used here is rebuilt from the text: Component tracking → Receive → Delivery, Admin reversal at any step, and Reports read from history.

**Scope in 10 bullets**

1. A barcode-based system that follows one physical **unit** (product barcode) through component tracking → receiving → delivery.
2. Five roles, **one role per user**. The **server** gates every screen and action; a hidden menu item is not security.
3. Authentication: registration, login, logout, password hashing.
4. Admin panel: CRUD for product names, component names, receive points and delivery points, plus user creation and role changes.
5. Operations: attach component barcodes to a product (a component belongs to one product only), receive into stock at a receive point, and deliver from stock to a delivery point (in-stock units only).
6. An **append-only inventory transaction history** (barcode, event, point, user, timestamp, reason). Current stock status is **derived** from it and never stored as an editable value.
7. Admin-only reversals with a reason. Each one adds a history row and has a defined side effect.
8. Reports built from history: stock details by current receive point, a date-range summary with a graph, and barcode-wise full history including reversals.
9. Additional: search, filter and pagination everywhere; CSV/Excel export; a separate audit log; deactivate instead of delete and soft delete; client and server validation with a duplicate-scan warning; dashboard; password change; forced logout of deactivated users.
10. Deliverables: flow diagram, ER diagram and SQL script with sample data, Git history, README, test-case sheet, user guide with screenshots, 10-minute demo **with slides**.

**Things in the PDF that the meta-prompt snapshot did not mention**

- The component row of the sample record uses the point **"Refrigerator Component Track Point 1"**. A third point type (**TRACK**) is therefore added to master data alongside RECEIVE and DELIVERY.
- The sample's **"By"** column holds employee numbers (66798, 58877, 54671). Users get an `employee_no` field, and the seeded users carry exactly those numbers.
- The PDF's project cycle is **18 working days in 7 phases**. This plan compresses that into **6 agent-assisted days**; the phase mapping is in Section 2. The intern should confirm this with the mentor.
- The PDF mentions "challenge sets" but defines none. They are **out of scope**.
- The presentation phase asks for a **live demo and slides**, so slides are produced on Day 6.

**ASSUMPTIONS** (each one is recorded in `DECISIONS.md` on Day 1)

- **A1. Unit barcodes.** A product barcode identifies one physical unit (a serial number), not a SKU. The first scan of an unknown barcode, at tracking or receiving, registers the unit and requires choosing its product name. Later scans show the product as read-only.
- **A2. When components can be tracked.** Component tracking is allowed before receiving and while the unit is in stock. It is blocked after delivery.
- **A3. When a unit can be received.** Receiving is allowed only when the unit is neither in stock nor delivered. There are no stock transfers between receive points. A delivered unit cannot be received again unless its delivery is reversed.
- **A4. Corrections.** A correction is a reversal followed by re-entering the right operation. After a reversal succeeds, the screen links to the operation screen with the barcode pre-filled.
- **A5. Reversal rows.** A reversal is a new row with `event_type = REVERSED` that points at the row it cancels. A row can be reversed at most once, a reversal can never itself be reversed, and the reason must be at least 5 characters.
- **A6. Registration.** Self-registration creates a **Report Viewer** account that stays **inactive until an Admin activates it**, because new accounts get least privilege. Admins can also create users directly with any role.
- **A7. Who sees what.** Reports and the transaction history are for Admin and Report Viewer. The audit log is Admin-only because it contains user data. The dashboard is read-only and visible to every signed-in role. Operational roles land on their own scan screen.
- **A8. Duplicate-scan warning.**
  - Client side: the same barcode scanned twice in the current form or batch shows an inline warning and is not added again.
  - Server side: a scan that repeats a state the unit is already in returns a warning saying when, where and by whom, and saves nothing. Examples are receiving a unit that is already in stock, or attaching a component that is already on this unit.
  - Rule violations are hard errors. Examples are a component that belongs to another unit, or delivering a unit that is not in stock.
- **A9. Soft delete.** A master-data row referenced by any unit or history row is soft-deleted: hidden from dropdowns, still shown in history and reports, and restorable. An unreferenced row is hard-deleted after confirmation. Users are never deleted.
- **A10. Scale.** The project runs at demo scale (hundreds of units). Status is derived in C# from history rows (EF Core queries), with no cached status column.
- **A11. Scanners.** A USB/HID barcode scanner behaves like a keyboard and sends Enter at the end, and manual typing works the same way. There is no camera scanning.
- **A12. Time zone.** All times are stored in UTC (`timestamptz`) and sent to the browser as ISO 8601 UTC. The browser shows them in the zone set in `appsettings.json` → `App:TimeZone` (exposed by `GET /api/config`). Seeded times are read in that zone, so reports show exactly 10:00:00, 10:15:00 and 11:30:00.
- **A13. Barcode format.** Barcodes are trimmed and upper-cased, and must match `^[A-Z0-9-]{6,32}$`.
- **A14. Database.** PostgreSQL 17 (decided 18 Nov 2026), running locally on port 5432. The app connects as its own role `ims_user`, which owns the `ims` database and may create databases, because the tests use a separate `ims_test` database.
- **A15. Architecture.** A **React single-page app** (Vite + TypeScript) talking to an **ASP.NET Core Web API** on .NET 10 LTS (decided 18 Nov 2026). Both are served from **one origin**: in development Vite proxies `/api` to the API; for the demo the React build is served by the API itself. Login uses the ASP.NET Core Identity **cookie** (HttpOnly, SameSite=Strict) plus an antiforgery header, never tokens in localStorage. Not Next.js: a second server would duplicate the .NET backend.

---

## 1. Recommended Stack & Why (≤5 lines)

**Frontend:** React 19 + TypeScript + Vite, React Router, TanStack Query, react-hook-form + zod, Bootstrap 5, Chart.js. **Backend:** .NET 10 LTS ASP.NET Core Web API (controllers) + ASP.NET Core Identity + EF Core 10 with Npgsql. **Database:** PostgreSQL 17. Tests: xUnit (API), Vitest (UI), Playwright (screenshots).

1. React + TypeScript is the most asked-for frontend skill; Vite gives instant reloads with no configuration, and TypeScript catches mistakes the way a C++ compiler does.
2. ASP.NET Core Identity gives login, salted password hashing, cookies and security stamps; authorization policies map one-to-one onto the role matrix and protect every API endpoint.
3. One origin and cookie auth keep security simple: no tokens in the browser, no CORS, and a deactivated user is cut off on their next request.
4. EF Core migrations build the PostgreSQL schema from C# classes, and `dotnet ef migrations script` produces the SQL deliverable; snake_case naming makes the tables match the design document.
5. Everything runs locally on Windows: two terminals while developing, one `dotnet run` for the demo after `npm run build`.

---

## 2. How to Use This Plan

**Already installed on this machine (18 Nov 2026):** Git, GitHub CLI (signed in), .NET SDK 10.0.404, `dotnet-ef` 10.0.15, **Node.js 26.13.1 LTS with npm 11.20**, VS Code with C# Dev Kit, PostgreSQL 17.12 (service running, tools on PATH), Claude Code. The database `ims` and the role `ims_user` exist.

**One-time setup still to do before Day 1.** Create an **empty** GitHub repository (no README or licence) named `inventory-management-system`, clone it to `C:\Projects\inventory-management-system`, and do one test push.

**Database connection (Day 1, once).** The connection string is a .NET **user secret**: it lives in your Windows profile, outside the repository, so it can never be committed. When the Day 1 agent stops and asks, run this yourself in the project folder, with your `ims_user` password (never paste it into the chat):

    dotnet user-secrets set "ConnectionStrings:Ims" "Host=localhost;Port=5432;Database=ims;Username=ims_user;Password=<your ims_user password>" --project src/Ims.Api

**Every day**

1. Open a **fresh** agent session in the project folder.
2. Paste the **full prompt**: the Project Context Block (Section 3) followed by that day's prompt. On the published page, the **Copy full prompt** button copies both together. The `prompts/day-N.md` files have them pre-joined.
3. Let the agent work through the task list. It starts with a *starting-state check*: clean tree, previous `day-(N-1)` tag present, builds and tests green.
4. Walk the **acceptance criteria** yourself in the browser, using the seeded logins.
5. Read the day's entry in `docs/LEARNING_NOTES.md` and open the files it points to: that is your .NET and React lesson for the day.
6. The agent runs the day's **commit & push** block and tags the day.

**Git flow.** Work on `main` with small commits, and tag every finished day (`day-1` … `day-6`, then `v1.0.0`). The tags make a readable history for the mentor and safe restore points.

**Running the app**

    Developing (two terminals; React reloads instantly):
      terminal 1:  dotnet run --project src/Ims.Api --launch-profile http     (API on http://localhost:5080)
      terminal 2:  npm run dev --prefix client                               (open http://localhost:5173)

    Demo (one process, like production):
      npm run build --prefix client                                          (writes src/Ims.Api/wwwroot)
      dotnet run --project src/Ims.Api --launch-profile http                 (open http://localhost:5080)

**Verifying any day**

    git checkout day-N
    dotnet build
    npm ci --prefix client
    dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed
    dotnet test
    npm test --prefix client
    then run the app as above

**Resetting**

- **Bad partial day** (nothing pushed yet): `git reset --hard day-(N-1)`, then `git clean -fd` after reviewing what it would delete with `git clean -nd`. Then re-run the day's prompt.
- **Corrupt or dirty database**: stop the API, then run `dotnet ef database drop --force --project src/Ims.Api`, `dotnet ef database update --project src/Ims.Api` and `dotnet run --project src/Ims.Api -- seed`. The history table is append-only by design, so a reset always means rebuilding the database.
- **Day overruns**: never push a broken build. Commit only what builds and passes, and list unfinished items under **Carry-over** in `DECISIONS.md`. Each day's prompt tells the agent to clear Carry-over first.

**How the PDF's 18-day project cycle maps onto 6 days**

| PDF phase (planned days) | Where it happens in this plan |
|---|---|
| 1. Requirement analysis (1) | Section 0 of this pack, and `DECISIONS.md` on Day 1 |
| 2. Process flow diagram (2) | Draft on Day 1, final on Day 5, PNG/PDF export on Day 6 |
| 3. Database design (2) | Design PDF already made; schema and ER on Day 1, final ER and SQL script on Day 5 |
| 4. Development (10) | Days 1–5 |
| 5. Testing (1) | Automated tests every day, then the test-case sheet and fixes on Day 6 |
| 6. Documentation (1) | Day 6 (README and learning notes updated every day) |
| 7. Presentation (1) | Day 6 demo script and slides; the live demo follows |

---

## 3. Reusable Project Context Block

Paste this at the top of every daily prompt.

```context
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
```

---

## 4. Day 1: Skeleton End-to-End

### Daily Prompt

```prompt
>>> PASTE THE PROJECT CONTEXT BLOCK (SECTION 3) HERE <<<

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
```

---

## 5. Day 2: Admin Panel, Users & Account Security

### Daily Prompt

```prompt
>>> PASTE THE PROJECT CONTEXT BLOCK (SECTION 3) HERE <<<

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
```

---

## 6. Day 3: Operations (Component Tracking, Receive, Delivery)

### Daily Prompt

```prompt
>>> PASTE THE PROJECT CONTEXT BLOCK (SECTION 3) HERE <<<

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
```

---

## 7. Day 4: Reversals, Reports & Export

### Daily Prompt

```prompt
>>> PASTE THE PROJECT CONTEXT BLOCK (SECTION 3) HERE <<<

=== DAY 4 OF 6: ADMIN REVERSALS, REPORTS, CSV/EXCEL EXPORT ===

1. ROLE
You are a senior full-stack engineer with a reporting/analytics focus, careful about data
correctness and side effects. Show the intern how LINQ projections, a small export service and a
React chart fit together.

2. CONTEXT & CURRENT STATE
Days 1–3 are done (tags day-1..day-3): append-only history with derived status, Identity cookie +
policies, admin panel (soft delete, activation, forced logout, password change), working
Track/Receive/Deliver with duplicate-scan warnings and a DB safety-net trigger, a filterable
/history, the shared list contract (export still returns 501). The reverse endpoints, the three
report endpoints and their React pages are still stubs.
MUST NOT REGRESS: all operations rules and tests, policy matrix, sample record, admin panel.
Starting-state check:
    git checkout main && git pull ; git tag --list "day-*"  -> day-1..day-3
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Clear any Carry-over items in DECISIONS.md first.

3. TODAY'S OBJECTIVE
Add admin-only reversal/correction with correct side effects, the three history-based reports
(stock details, date-range summary with a chart, barcode-wise history), and CSV/Excel export for
every list and report. Advances: Core 7, Core 8; Additional 1 (reports), 2.

4. ORDERED TASK LIST
 1. InventoryService.ReverseAsync(rowId, reason, admin) per R7–R10 in one locked transaction:
    reason >= 5 chars; target not already reversed and not a REVERSED row; RECEIVED only if the
    unit is not Delivered; DELIVERED only if it is the latest effective delivery (new row's point
    = the receive point the unit returns to); COMPONENT_TRACKED -> detach. Writes one REVERSED
    row. CanReverseAsync(row) -> (allowed, reason) and a preview text.
 2. API: GET /api/history/{id}/reverse-preview (history.reverse) -> {row, allowed, blockedReason,
    effect}; POST /api/history/{id}/reverse {reason}. GET /api/history rows now include
    {reversed, reversedBy, reversedAt, canReverse} (canReverse only computed for admins).
 3. React: on /history (admin only) a Reverse button on rows where canReverse is true (others a
    disabled button with a tooltip); /history/:id/reverse shows the original row, the effect
    preview (e.g. "Unit 2440701AK2057 will return to stock at Refrigerator Receive Point 1") and a
    required reason textarea (zod min 5). On success: a message + "Re-enter the correct entry"
    link to the relevant ops page with ?barcode= pre-filled (ScanPanel reads it). Reversed rows
    are struck through with "reversed by X on Y".
 4. ExportService: ToCsv (RFC 4180, UTF-8 with BOM) and ToXlsx (ClosedXML: bold header, adjusted
    widths, frozen header, auto-filter). Every list endpoint with format=csv|xlsx returns
    File(...) named <report>_<yyyyMMdd-HHmm>.<ext>, using the same filters, no paging, local
    times. React ExportButtons build the same URL from the current list params and trigger the
    download with a normal link (same-origin cookie, no fetch needed). Enable on /history, all
    reports and admin lists.
 5. Reports API (ReportService, reports.view) + React pages:
    - /reports/stock: one row per InStock unit: barcode, product, current receive point, received
      at, received by, days in stock, number of attached components; summary chips per receive
      point; filters product, receive point, minimum days; search barcode; sort by days; paging;
      export.
    - /reports/summary: from/to (default last 30 days; server validates from <= to and range <=
      366 days with field errors); optional point/product filters; totals cards (tracked,
      received, delivered, reversed); a react-chartjs-2 grouped bar chart per day (received vs
      delivered, + tracked) using Bootstrap's CSS variables for colours; per-day table (paged) +
      export.
    - /reports/barcode: input a product barcode OR a component barcode (a component resolves to
      the unit(s) it was ever attached to; if more than one, let the user pick); header card
      (product, status badge, current point, attached components) and a vertical timeline of ALL
      rows in order, including reversed rows (struck through, linked to their reversal) and
      REVERSED rows with reason and admin; export.
 6. REPORT_VIEWER sees everything read-only (no Reverse buttons, no write forms).
 7. Tests (API): each reversal rule (allowed and blocked, incl. reverse receive after delivery ->
    blocked; reverse delivery -> back in stock at the original receive point; reverse component ->
    re-trackable on another unit; double reversal blocked; non-admin -> 403; short reason -> 400/
    422); stock report counts; summary 2026-03-27..2026-03-30 -> 1 tracked, 1 received, 1
    delivered; barcode history for 2440701AK2057 = 3 rows; CSV/XLSX content type, header row and
    filters honoured (open the XLSX with ClosedXML in the test). Vitest: ExportButtons build the
    right URL from list params; the reverse form blocks a short reason.
 8. README "Reports & export"; DECISIONS.md (reversal-point semantics); LEARNING_NOTES.md Day 4
    (LINQ GroupBy/projections, records as DTOs, returning files from controllers; React: charts
    with react-chartjs-2, derived UI state, optimistic vs refetch after mutations).

5. ACCEPTANCE CRITERIA
- [ ] Admin reverses the sample DELIVERED row with a reason -> 2440701AK2057 is InStock at
      "Refrigerator Receive Point 1"; stock report and dashboard show it
- [ ] Trying to reverse its RECEIVED row while it is delivered is blocked with an explanation
- [ ] Reversing a COMPONENT_TRACKED row lets that component barcode be attached to another unit
- [ ] A row cannot be reversed twice; a REVERSED row has no Reverse button; receiver/viewer get
      403 on the reverse endpoint and the Forbidden page on the route
- [ ] Barcode history for 2440701AK2057 shows the full path including the reversal and reason
- [ ] Summary for 2026-03-27..2026-03-30 shows 1/1/1 and a rendered chart
- [ ] CSV and Excel downloads open correctly in Excel and match the on-screen filters
- [ ] All tests pass (earlier + today's)

6. VERIFICATION
    dotnet build && dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed
    dotnet test && npm test --prefix client && npm run build --prefix client   -> green
    run in dev mode and open http://localhost:5173
Manual: as admin reverse the sample delivery, check the stock report, then reset the database
(dotnet ef database drop --force, database update, seed) so the pristine sample record is back.
As viewer, open all three reports and export each one.

7. DEMO NOTE
Show barcode history for 2440701AK2057 (3 rows), reverse its delivery as admin, refresh to show the
4th row (REVERSED, with reason) and the unit back in stock; then download the Excel export.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "feat(reports): admin reversals, stock/summary/barcode reports with chart and CSV/Excel export"
    git push origin main
    git tag -a day-4 -m "Day 4: reversals, reports, export"
    git push origin day-4

9. GUARDRAILS
- Do not break prior features; operations rules and their tests must stay green.
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Reversals ADD rows; they never modify or delete existing rows.
- Every report number comes from history via the services, never from a stored counter.
```

---

## 8. Day 5: Audit Log, Dashboard, Demo Data, Final Diagrams & SQL

### Daily Prompt

```prompt
>>> PASTE THE PROJECT CONTEXT BLOCK (SECTION 3) HERE <<<

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
```

---

## 9. Day 6: QA, Documentation, Demo & Submission

### Daily Prompt

```prompt
>>> PASTE THE PROJECT CONTEXT BLOCK (SECTION 3) HERE <<<

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
```

---

## 10. Traceability Table

| # | Requirement (from the brief) | Day | Acceptance signal |
|---|---|---|---|
| C1 | Authentication: register, login, logout, hashing, one role per user | 1 (Day 2 adds activation of registrations) | Seeded users log in via the Identity cookie; registration creates an inactive Report Viewer; salted password hashes; one `role` column per user |
| C1a | Server-side enforcement of every screen and action | 1 | Viewer calling a receive endpoint gets 403 (a test covers every role × endpoint); the React route shows Forbidden |
| C2 | Admin: CRUD products, components, receive points, delivery points (+ track points) | 2 | Admin creates, edits, deletes and restores each type; uniqueness errors on the field |
| C2a | Admin: create users and change their roles | 2 | New user with a role; role change takes effect at the next request |
| C3 | Receive at a receive point | 3 | Receiver receives TESTUNIT0001 → InStock at the chosen point |
| C4 | Component tracking; a component belongs to one product only | 3 | Attaching 94A600321852 to another unit is rejected with the owner's barcode (service + DB trigger) |
| C5 | Delivery of in-stock units only; the unit leaves the stock count | 3 | Delivering a Registered or Delivered unit fails; stock count drops after delivery |
| C6 | Append-only history; status derived from history | 1 (base), 3 (all events) | Interceptor and PostgreSQL trigger tests block UPDATE/DELETE; no status column; `GetUnitStatusAsync` tests |
| C7 | Admin reversal with reason, plus the three side-effect rules | 4 | Reverse delivery → in stock at the original receive point; reverse receive blocked after delivery; reverse component → re-trackable; non-admin 403 |
| C8a | Report: stock details by current receive point | 4 | `/reports/stock` lists in-stock units with point, days in stock, per-point chips |
| C8b | Report: date-range summary with a graph | 4 | 2026-03-27..30 shows 1/1/1 and a Chart.js chart |
| C8c | Report: barcode-wise full path, including reversals | 4 | 2440701AK2057 shows 3 rows (4 after a reversal, with its reason) |
| A1 | Search, filter, pagination on every list and report | 2 (admin lists), 3 (history), 4 (reports), 5 (sweep + audit) | Sweep checklist in DECISIONS.md; every list uses the shared list contract and URL params |
| A2 | Export reports to CSV/Excel | 4 | CSV/XLSX download matches the on-screen filters; export tests open the XLSX |
| A3 | Separate audit log: data changes, user changes, logins, old/new values | 5 | Product rename shows old→new; LOGIN, LOGOUT and LOGIN_FAILED rows; append-only test |
| A4 | Activate/deactivate users; soft delete of in-use master data | 2 | Deactivate instead of delete; "Refrigerator" archived, not deleted |
| A5 | Client + server validation, clear errors, duplicate-scan warning | 2 (admin forms), 3 (scan forms) | zod + server DataAnnotations; field errors mapped with setError; batch duplicate warning; 409 duplicate-scan alert |
| A6 | Dashboard: stock count, today's receives/deliveries, longest-stuck | 1 (two counts), 5 (full) | Stat cards and the stuck-longest list populated with demo data |
| A7 | Password change; forced logout of deactivated users | 2 | Two-window forced logout demo (security stamp → 401 → login page); the old password stops working |
| SD | Sample record seeded exactly (3 rows, in order) | 1 (seed), 5 (end-to-end test) | `SampleRecordEndToEnd` green; status Delivered |
| D1 | Process flow diagram (image or PDF) | 1 (draft), 5 (final), 6 (PNG/PDF) | `docs/diagrams/process-flow.{md,png,pdf}` |
| D2 | ER diagram + DB script with sample data | 1 (draft), 5 (final + SQL), 6 (PNG/PDF) | `docs/diagrams/er-diagram.*`; `docs/db/schema.sql` (EF migrations script) + pg_dump with 3 rows |
| D3 | Git repo with commit history | 1–6 | Daily Conventional Commits, tags `day-1`…`day-6`, `v1.0.0` |
| D4 | README: setup, default admin, one login per role, stack rationale | 1 (v1), updated each day, 6 (final) | A fresh clone works from README alone, in dev and demo mode |
| D5 | Test-case sheet with expected/actual/pass-fail | 6 (automated tests every day) | `docs/testing/test-cases.xlsx`, 45+ executed cases |
| D6 | User guide with screenshots of every module | 6 | `docs/user-guide/USER_GUIDE.md` + `img/` (Playwright) |
| D7 | 10-minute demo (+ slides) | 6 | `docs/demo/DEMO_SCRIPT.md`, `docs/demo/slides.md`, rehearsed |

---

## 11. Submission Checklist

| PDF deliverable | What exists | Where | Built on |
|---|---|---|---|
| Process flow diagram (image or PDF) | Mermaid source + rendered PNG and PDF with role swimlanes | `docs/diagrams/process-flow.md`, `.png`, `.pdf` | Day 1 → 5 → 6 |
| ER diagram | Mermaid erDiagram matching the database + PNG/PDF | `docs/diagrams/er-diagram.md`, `.png`, `.pdf` | Day 1 → 5 → 6 |
| Database script with sample data loaded | EF Core migrations script (tables, indexes, constraints, triggers) + pg_dump with the 3-row sample | `docs/db/schema.sql`, `docs/db/inventory_with_sample_data.sql`, `docs/db/README.md` | Day 5 |
| Source code in Git with a commit history | GitHub repo, Conventional Commits, tags | `main`, tags `day-1`…`day-6`, `v1.0.0` | Days 1–6 |
| README | Setup (.NET 10, Node, PostgreSQL, user-secrets), dev + demo run modes, stack + why, default admin + one login per role, reset, tests, docs index | `README.md` | Day 1 → 6 |
| Test-case sheet (case, expected, actual, pass/fail) | 45+ executed cases mapped to xUnit, Vitest and Playwright tests | `docs/testing/test-cases.xlsx` (+ `.csv` source) | Day 6 |
| User guide with screenshots of every module | Per-role guide, every module screenshotted | `docs/user-guide/USER_GUIDE.md`, `docs/user-guide/img/` | Day 6 |
| Demo video or live demo (10 min) | Timed script, pre-demo checklist, slides | `docs/demo/DEMO_SCRIPT.md`, `docs/demo/slides.md` | Day 6 |
| Stack named in README with a reason | 5-line rationale + architecture diagram | `README.md` → "Stack" | Day 1 |
| Decisions and open questions | Assumptions A1–A15 + daily decisions | `DECISIONS.md` | Days 1–6 |
| Learning record (extra) | Daily notes on the .NET and React concepts used, with file references | `docs/LEARNING_NOTES.md` | Days 1–6 |
| Submission summary | Deliverable → path → status, repo URL, review tag | `docs/SUBMISSION.md` | Day 6 |

**Before you submit:** run the demo script once against a freshly reset database in demo mode, confirm `git status` is clean and `v1.0.0` is pushed, then send the repository link plus `docs/` (or the PDF exports) to the mentor by the end of the last working day.
