(Project context: CLAUDE.md in this folder is loaded automatically and is the authoritative
project specification. Follow it. The full plan is docs/planning/IMS_Build_Plan.md.)

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
