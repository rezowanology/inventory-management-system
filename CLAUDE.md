# CLAUDE.md — Inventory Management System (IMS)

> Read this file first. It is the project's memory: who you are working with, what was decided and why,
> what is installed on this machine, the hard rules, and the full specification. Last updated 19 Nov 2026.
> The same material lives in more detail in `docs/planning/IMS_Build_Plan.md` (the 6-day plan + prompts).

---

## 1. The project in one paragraph

A **barcode-based inventory system** that tracks one physical unit (identified by its product barcode) from
**component tracking → receiving → delivery**, with **five roles** (one per user, enforced on the server),
an **append-only inventory history** (current stock is derived from it, never stored), admin-only
**reversals**, **reports** (stock, date-range summary with a graph, barcode-wise history), CSV/Excel
**export**, an **audit log**, a **dashboard**, and activate/deactivate users. It is the intern's
**industrial attachment assignment** (brief dated 3 Nov 2026, saved as `docs/planning/Assignment-Brief.pdf`).
It is a **learning/demo project**: a working, presentable end-to-end system matters more than
production hardening. No over-engineering.

## 2. Who you are working with

- The user is the **intern** (Rezowan Rahman Raj). Background: **C++**. This week they are **learning C#, .NET
  and React** while building this. A **team lead / mentor** reviews progress and gets status updates; the
  assignment says to commit at least daily and show progress at the end of each phase.
- **How to talk to them**
  - Plain English, beginner-friendly. Explain jargon the first time (they asked "what is PK, FK, UQ").
  - Say what you did, what passed, what did not. Never claim something works unless you ran it.
  - Give exact commands and file paths. They like being told the next concrete step.
  - They often ask you to **do setup for them** (installs, PATH, config). Do it when it is safe and local;
    for anything needing admin rights, a password, or outward effects, give the command and let them run it.
- **Teaching is part of the job.** Write clear, idiomatic C# and React; add short comments where a framework
  idiom first appears; and append a daily entry to `docs/LEARNING_NOTES.md` (see §6).
- Deliverables the assignment requires (all must exist by the end): process flow diagram (image/PDF) · ER
  diagram + DB script with sample data · source in Git with real commit history · README (setup, default
  admin login, **one login per role**, stack named with reasons) · test-case sheet (case, expected, actual,
  pass/fail) · user guide with screenshots of every module · 10-minute demo (live or video) + slides.

## 3. Current status (as of 19 Nov 2026)

**Done**
- Requirements analysed; 6-day plan and prompt pack written (`docs/planning/IMS_Build_Plan.md`).
- Database designed and **tested on the real PostgreSQL 17** (`docs/planning/db/`), with a PDF for the team
  lead (`IMS_Database_Design.pdf`: ER diagram, relationships, rules, full SQL, tests).
- A proposal doc (roles + database design) was written for the team lead.
- Machine fully set up (see §4): .NET 10, Node, PostgreSQL 17 with the `ims` database and `ims_user` role, Git,
  GitHub CLI signed in, VS Code with C# Dev Kit and Claude Code, Claude Code CLI.

**Not done yet**
- No application code exists. **Day 1 has not been run.**
- The GitHub repository `inventory-management-system` is not created/cloned/pushed yet (visibility undecided).
- This folder (`C:\Projects\inventory-management-system`) has **no `.git` yet** (run `git init -b main` and add
  the remote — see §11).
- The user has not yet stored the database password as a .NET user-secret (Day 1 does this; see §5).
- Decisions still to confirm with the mentor (see §9).

**What is in this folder right now**: `CLAUDE.md` and `docs/planning/` only. **Keep `docs/planning/`** — do not
delete or restructure it when scaffolding. Day 5 creates a separate `docs/db/` for the final deliverables.

## 4. Machine and environment (verified 18 Nov 2026)

| Item | State |
|---|---|
| OS | Windows 11 Pro. Primary shell **Windows PowerShell 5.1**; **Git Bash** also available (Claude Code's Bash tool) |
| Time zone | **Bangladesh Standard Time (UTC+06:00, Dhaka)** → use `App:TimeZone = "Asia/Dhaka"` (IANA id; fall back to the Windows id `Bangladesh Standard Time` if `TimeZoneInfo.FindSystemTimeZoneById` rejects it) |
| .NET | **SDK 10.0.404** (`dotnet --list-sdks`). Global tool **dotnet-ef 10.0.15** (`C:\Users\ASUS\.dotnet\tools`, on PATH). ASP.NET dev HTTPS certificate exists but is **not trusted** → always run the API with the **`http` launch profile** (`http://localhost:5080`) |
| Node | **Node.js 26.13.1 LTS**, **npm 11.20.0** |
| PostgreSQL | **17.12**, Windows service `postgresql-x64-17` (automatic start), **localhost:5432**. Tools on user PATH: `psql`, `createdb`, `dropdb`, `pg_dump` (`C:\Program Files\PostgreSQL\17\bin`) |
| Database | `ims` owned by role **`ims_user`** (LOGIN, **CREATEDB** so tests can create `ims_test`). Created by `docs/planning/scripts/setup-ims-database.ps1` (idempotent) |
| Git | Git 2.52 for Windows, credential helper = Git Credential Manager. Global identity is already set (name **Rezowan Rahman Raj**; email is in `git config --global user.email` — do not write it into any file) |
| GitHub | GitHub CLI 2.104 signed in as **rezowanology** (HTTPS, scopes `repo`, `workflow`, `read:org`, `gist`). Planned repo: `rezowanology/inventory-management-system` |
| Editor / agent | VS Code 1.146 with C# Dev Kit, Python, and **Claude Code extension 2.1.327**; Claude Code CLI 2.1.325 (`C:\Users\ASUS\.local\bin`, on PATH) |
| Other | Microsoft Edge (`C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`) works headless for HTML→PDF/screenshots; `pdftotext` exists in Git Bash. Python 3.13 (Anaconda) exists but is **not used** by this project |

**Package versions resolved on 18 Nov 2026** (a throwaway scaffold with these built successfully; pin whatever
`dotnet add package` / `npm install` resolve on Day 1 and record them in README):
- NuGet: Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 · EFCore.NamingConventions 10.0.1 · ClosedXML 0.105.1 ·
  Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.x · Microsoft.EntityFrameworkCore.Design 10.0.x ·
  Microsoft.AspNetCore.Mvc.Testing 10.0.x
- npm: react 19.3.1 · vite 8.3.8 · typescript 7.0.2 · react-router 8.5.1 · @tanstack/react-query 5.108.0 ·
  react-hook-form 7.92.0 · zod 4.6.7 · @hookform/resolvers 7.0.7 · bootstrap 5.3.9 · chart.js 4.6.1 ·
  react-chartjs-2 5.3.1 · vitest 5.1.0 · @testing-library/react 16.3.3 · @playwright/test 1.65.1
  (`npm create vite@latest client -- --template react-ts` gives the base; it also adds oxlint.)

**Shell notes for this machine**
- Windows PowerShell 5.1 has **no `&&`/`||`** — chain with `;` or `if ($?) { }`, or use Git Bash. In PS 5.1, a
  native tool writing to stderr (psql NOTICE, dropdb messages) is treated as an error when you redirect `2>&1`;
  check `$LASTEXITCODE` instead. Running `.ps1` files may need `powershell -ExecutionPolicy Bypass -File ...`.
- Git Bash: use forward slashes (`/c/Projects/...`). Quote paths with spaces.
- Keep the project path **short and outside OneDrive** (it is `C:\Projects\inventory-management-system`).
- **.NET 10 gotcha:** `dotnet new sln` now creates a **`.slnx`** file by default. The prompts say `Ims.sln`. Either run
  `dotnet new sln -n Ims --format sln` (check `dotnet new sln --help`) or accept `Ims.slnx` and use that name in
  every command and doc; record the choice in `DECISIONS.md`.
- Installers that need administrator approval or a GUI cannot be driven reliably from here: give the user the
  `winget` command and let them run it.

## 5. Hard rules (security, secrets, outward actions)

1. **Never ask for, print, echo, log, store or commit any password**: not the `ims_user` password, not the
   `postgres` admin password. The connection string lives **only** in .NET **user-secrets** of `src/Ims.Api`
   (key `ConnectionStrings:Ims`) — never in `appsettings*.json`, `.env`, source, docs, tests or git.
   **Do not run `dotnet user-secrets list`** (it prints values). On Day 1 STOP and ask the user to run this
   themselves, then continue when they confirm:
   `dotnet user-secrets set "ConnectionStrings:Ims" "Host=localhost;Port=5432;Database=ims;Username=ims_user;Password=<their ims_user password>" --project src/Ims.Api`
   If a connection fails with "password authentication failed", the password was wrong: ask them to re-run the
   command (or run `docs/planning/scripts/setup-ims-database.ps1` to set a new one). Do not guess.
2. The demo seed passwords in §8 (`Admin@12345`, `Demo@12345`) are demo-only and may appear in the README
   (the assignment requires default logins). No real credential may ever appear in files.
3. Before every commit run `git status` and make sure no secrets, `bin/`, `obj/`, `node_modules/`,
   `src/Ims.Api/wwwroot/` build output or Playwright output are staged. After scaffolding, `git grep -i password`
   must show only documentation/demo text.
4. The daily prompt **authorizes** committing, pushing `main` and pushing the day tag for this project's repo.
   Anything else outward needs the user's OK first: **creating the GitHub repo or changing its visibility**,
   force-pushing, deleting remote branches/tags, changing global Git config, PATH, or system settings,
   installing system software.
5. **Never edit or delete rows of `inventory_transactions` or `audit_log`** — not in code paths, migrations,
   seeds or tests (tests may only attempt it to prove it fails). Reversals ADD rows.
6. Do not add: Next.js, Redux/Zustand, a React UI kit, Docker, MediatR, AutoMapper, generic repositories,
   JWT/localStorage auth, Blazor/Razor Pages, SQLite (not even for tests), or any other database.
7. Tests must run against a real PostgreSQL database `ims_test` (the fixture drops/creates it). Never point
   tests at `ims`.

## 6. How the work is organised

**Six days, one prompt per day.** Each day builds a slice, ends green, and is committed, pushed and tagged.

| Day | Theme | Ends with tag |
|---|---|---|
| 1 | Skeleton end-to-end: repo, schema + migrations, Identity cookie auth + policies, React shell, every route reachable, seed (sample record), tests, README | `day-1` |
| 2 | Admin panel: master-data CRUD with soft delete, user management, password change, forced logout | `day-2` |
| 3 | Operations: track / receive / deliver, rules in `InventoryService`, duplicate-scan warning, DB safety triggers, filterable history | `day-3` |
| 4 | Reversals, the 3 reports + chart, CSV/Excel export | `day-4` |
| 5 | Audit log, dashboard, demo data, final diagrams, SQL deliverables, sweep of search/filter/paging | `day-5` |
| 6 | QA: test-case sheet, user guide with screenshots, diagram images, demo script + slides, submission, `v1.0.0` | `day-6`, `v1.0.0` |

**To run a day (VS Code, Claude Code panel, folder `C:\Projects\inventory-management-system`):**
1. Start a **new** conversation (each day is a fresh session; this file gives the context).
2. Say: *"Carry out the Day N prompt in `docs/planning/prompts/day-N.md`."* (The lean prompts rely on this
   CLAUDE.md. Fully self-contained versions are in `docs/planning/prompts-full/` if ever needed.)
3. Do the **starting-state check** the prompt lists (clean tree, previous tag present, build + tests green) and
   clear the **Carry-over** list in `DECISIONS.md` first.
4. Build in the prompt's task order. Verify with the commands the prompt gives. Walk the **acceptance
   criteria** and tick them honestly.
5. Finish with the day's exact commit message, `git push origin main`, annotated tag `day-N`, push the tag.
6. Never push a broken build. If a day overruns, commit only what builds and passes and list the rest under
   **Carry-over** in `DECISIONS.md`. Realistically **Days 1, 3 and 4 are the heaviest** (every feature needs
   an API endpoint *and* a React screen).

**Living documents you maintain**
- `DECISIONS.md` — every judgment call (date, decision, reason) + a **Carry-over** section. Day 1 starts it by
  copying assumptions **A1–A15** (below, §7) and adding Day-1 choices.
- `docs/LEARNING_NOTES.md` — every day append a dated section: the .NET and React concepts used that day,
  2–3 sentences each with the file path, plain English for a beginner.
- `docs/PROGRESS.md` — (optional but wanted for the team lead) append 4–6 lines per day: date, what works, tests
  status, what is next, any risk.
- `README.md` — updated daily (setup, run modes, seeded logins, tests, reset, status "Day N of 6").

**Commit conventions:** Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`, `refactor:`), small
commits on `main` during the day, the day-ending commit message is given in each prompt. Tags: `day-1`…`day-6`,
then `v1.0.0`.

**Everyday commands** (full list in the spec's RUN section):

```
dotnet build
dotnet ef database update --project src/Ims.Api
dotnet run --project src/Ims.Api -- seed
dotnet run --project src/Ims.Api --launch-profile http        # API on http://localhost:5080
npm run dev --prefix client                                   # React on http://localhost:5173 (proxies /api)
npm run build --prefix client                                 # demo mode: API serves the build on :5080
dotnet test ; npm test --prefix client
dotnet ef database drop --force --project src/Ims.Api         # reset, then update + seed
```

## 7. Architecture and decision history

**Final architecture (decided 18 Nov 2026):** React 19 + TypeScript + Vite single-page app calling an
ASP.NET Core Web API (.NET 10 LTS, controllers) with ASP.NET Core Identity (cookie auth + antiforgery header),
EF Core 10 + Npgsql, PostgreSQL 17. **One origin**: Vite proxies `/api` in development; for the demo the API
serves the built React app. Full detail in the specification (§8).

**How we got here (so you understand the constraints):**

| Date | Decision | Why / notes |
|---|---|---|
| 16 Nov | Plan created for **Django + SQLite** | Easiest for a C++-only intern; built-in auth/validation |
| 16 Nov | Database design + ER diagram + proposal for the team lead | Roles, 7 tables, sample record mapping |
| 18 Nov | **PostgreSQL 17** chosen by the user (replaces SQLite) | More production-like; installed and set up |
| 18 Nov | User chose to learn **.NET** → **.NET 10 + Razor Pages** | Compared Django / .NET / Next.js; C# feels natural to a C++ developer |
| 18 Nov | **Frontend changed to React (Vite + TS)** with an ASP.NET Core Web API (replaces Razor Pages) | User's request. **Next.js was rejected**: it is a second server that would duplicate the .NET backend |

Superseded plans are in `C:\Users\ASUS\Downloads` (`IMS_Build_Plan.django.md`, `IMS_Build_Plan.razor-pages.md`,
`prompts-django/`, `prompts-razor-pages/`). **Do not use them.**

**Assumptions A1–A15** (copied from the plan; Day 1 copies these into `DECISIONS.md`):

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

## 8. Project specification (authoritative)

Everything in this block is binding for all days. (It is the same "Project Context Block" the day prompts
reference.)

```text
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

## 9. Open questions (confirm with the mentor / team lead)

- [ ] **Registration:** self-registered users become inactive Report Viewers until an Admin activates them (A6) — OK?
- [ ] **Audit log access:** Admin only (A7) — or also Report Viewer?
- [ ] **Delivered units:** may a delivered unit be received again (a return), or only after its delivery is reversed (A3)?
- [ ] **Time zone** for stored/shown times: assumed `Asia/Dhaka` (A12).
- [ ] **Timeline:** the brief plans 18 working days; this build plan uses 6 agent-assisted days.
- [ ] **Repository visibility** (private/public) and whether to add the mentor as a collaborator.
- [ ] The "challenge sets" mentioned in the brief are **not defined** in it → treated as out of scope.

## 10. Known pitfalls (learned while planning — do not repeat)

- **PL/pgSQL:** a `CASE ... WHEN ... THEN` placed inside an `IF ... THEN` condition ends the condition at its
  first `THEN` (syntax error). Compute the value into a variable first, then compare. The reference function in
  `docs/planning/db/ims_schema_postgres.sql` (`check_transaction()`) already does this.
- **PostgreSQL identity ids are never reused** (a failed insert consumes an id). Never assert exact next ids in
  tests; assert ordering or that ids are greater than the seeded ones.
- **Reference SQL vs EF migrations:** `docs/planning/db/ims_schema_postgres.sql` is the tested *reference design*
  (tables, CHECKs, partial unique indexes, `forbid_change()`, `check_transaction()`, views, sample data). The real
  schema comes from EF Core migrations and must match its table/column names, constraints and triggers (Identity
  adds extra columns to `users`). `inventory_transactions.point_id` is NOT NULL: a reversal row records the point
  involved (R9 defines it). Reuse the trigger SQL via `migrationBuilder.Sql` (with `Down()`).
- **Reference tests:** `docs/planning/db/ims_schema_tests.sql` + `test-schema.ps1` load the reference schema into a
  throwaway database and run 18 checks (17 passed on the first real run on 18 Nov; the 18th, an exact-id check, was
  then corrected — see the identity-ids pitfall above); useful as a model for the xUnit trigger tests.
- **Time:** store UTC (`timestamptz`); the seeded sample times (10:00:00, 10:15:00, 11:30:00) are *local* times in
  `App:TimeZone`, so they are stored as 04:00, 04:15, 05:30 UTC in Dhaka — and shown again as 10:00/10:15/11:30.
- **Cookies + SPA:** keep one origin (Vite proxy), `SameSite=Strict`, `/api` returns 401/403 instead of redirects,
  antiforgery header `X-XSRF-TOKEN`. Never put tokens in `localStorage`.
- **Forced logout:** `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero` + `UpdateSecurityStampAsync`
  on deactivation; also `CanSignInAsync` must refuse inactive users.
- **Barcode regex** must be identical on server (`Barcode.Pattern`) and client (`lib/barcode.ts`); pin it with a test
  on each side.
- **Seeding:** `-- seed` is a command-line argument handled in `Program.cs` (seed then exit). It expects
  `dotnet ef database update` to have run.
- **Windows:** use `http://localhost:5080` (not https) so no certificate prompt appears during the demo.

## 11. Next actions (in order)

1. In `C:\Projects\inventory-management-system` (this folder) initialise Git and connect the GitHub repo:
   - Create an **empty** GitHub repo named `inventory-management-system` (no README/licence) — the user decides
     visibility — e.g. `gh repo create inventory-management-system --private --source . --remote origin`
     (or create it on github.com and run `git remote add origin https://github.com/rezowanology/inventory-management-system.git`).
   - `git init -b main` (if there is no `.git` yet), then commit `CLAUDE.md` + `docs/planning/` as
     `chore: add project plan, database design and CLAUDE.md`, and `git push -u origin main`. This also proves
     pushing works before Day 1.
2. (Optional) Verify the GitHub commit email is added/verified on the GitHub account so commits link to the profile.
3. Open this folder in VS Code (trust it), start a new Claude Code conversation, and run **Day 1**.
4. At the Day 1 secrets step, the user runs the `dotnet user-secrets set ...` command themselves (§5).
5. After each day: read that day's `docs/LEARNING_NOTES.md` entry, tick the acceptance list, and send the team
   lead the short entry from `docs/PROGRESS.md`.

## 12. Reference material in this repo and elsewhere

| What | Where |
|---|---|
| Full plan: scope, assumptions, stack, how-to, context block, 6 prompts, traceability table (requirement → day), submission checklist | `docs/planning/IMS_Build_Plan.md` |
| Day prompts (lean, rely on this file) | `docs/planning/prompts/day-1.md` … `day-6.md` |
| Day prompts (fully self-contained) | `docs/planning/prompts-full/day-1.md` … `day-6.md` |
| Original assignment | `docs/planning/Assignment-Brief.pdf` |
| Database design PDF for the team lead | `docs/planning/db/IMS_Database_Design.pdf` |
| Tested reference SQL (PostgreSQL 17) + tests | `docs/planning/db/ims_schema_postgres.sql`, `ims_schema_tests.sql`, `test-schema.ps1` |
| Database/role setup script | `docs/planning/scripts/setup-ims-database.ps1` |
| Published plan page with copy buttons (private to the owner) | https://claude.ai/artifact/GwyD1FULnYbi8ii3aJQKEF |
| Proposal doc: roles + database design (private to the owner) | https://claude.ai/artifact/6v4mBynvsiU87DEPiMLCay |

### Requirements checklist (from the brief; every item maps to a day in the plan's traceability table)

**Core:** (1) auth: register, login, logout, hashing, one role per user · (2) admin panel: CRUD product, component,
receive-point, delivery-point names; create users, change roles · (3) receive at a receive point · (4) component
tracking (a component barcode belongs to one product only) · (5) delivery of in-stock units only · (6) append-only
history, status derived · (7) admin reversal with reason: reverse a receive only if not delivered; a reversed
delivery returns to its original receive point; a reversed component entry detaches the component · (8) reports:
stock details by current receive point, date-range summary with a graph, barcode-wise full history incl. reversals.

**Additional:** (1) search/filter/pagination everywhere · (2) CSV/Excel export · (3) audit log (changes + logins,
old/new values, separate from inventory history) · (4) activate/deactivate users, soft-delete in-use master data ·
(5) client + server validation, clear errors, duplicate-scan warning · (6) dashboard (stock count, today's receives
and deliveries, longest-stuck stock) · (7) password change, forced logout of deactivated users.

**Sample record (must be seeded exactly, in this order):** unit `2440701AK2057` (Refrigerator): 2026-03-27 10:00:00
component tracked (Compressor `94A600321852`, Refrigerator Component Track Point 1, by 66798) → 2026-03-28 10:15:00
received (Refrigerator Receive Point 1, by 58877) → 2026-03-30 11:30:00 delivered (Refrigerator Delivery Point 1,
by 54671). Afterwards the status is Delivered and the barcode history shows all three rows.
