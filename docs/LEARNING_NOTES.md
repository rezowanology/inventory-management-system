# Learning notes

Plain-English notes on the .NET and React ideas used each day, with the file where you can see them. You come from C++,
so comparisons to C++ are included where they help.

## Day 1 — 22 Nov 2026

### .NET

- **Solution layout.** `Ims.sln` is just a list of projects (like a CMake top-level file). `src/Ims.Api` is the web API;
  `tests/Ims.Tests` references it. `Directory.Build.props` applies settings (nullable checking, implicit `using`s)
  to every project; `global.json` pins the SDK version.
- **Program.cs and dependency injection (DI).** `src/Ims.Api/Program.cs` is the "composition root". You *register*
  services (`AddScoped<InventoryService>()`) and ASP.NET *injects* them into constructors. Think of it as a factory that
  builds objects and hands each class what it asks for, instead of you calling `new` everywhere. "Scoped" = one instance
  per HTTP request.
- **Primary constructors.** `class InventoryService(ImsDbContext db)` (C# 12) declares the constructor parameters right
  in the class header; `db` is usable in every method. See `Services/InventoryService.cs`.
- **Controllers and attributes.** A controller class (`Controllers/HistoryController.cs`) maps URLs to methods.
  `[Route("api/history")]`, `[HttpGet]`, `[Authorize(Policy = ...)]` are *attributes*: metadata in square brackets that
  the framework reads to decide routing and security. `[ApiController]` adds automatic 400 answers for bad request bodies.
- **async/await.** Database calls return `Task<T>`; `await` pauses this method without blocking the thread. Almost
  every method that touches the database is `async`.
- **EF Core DbContext.** `Data/ImsDbContext.cs` is your gateway to the database. Each `DbSet<T>` is a table; LINQ
  (`db.Units.Where(...)`) is translated to SQL. The column details live in `Data/Configurations/*` (one class per table).
- **Migrations.** `dotnet ef migrations add` compares your C# model with the last snapshot and writes a C# file that
  creates/changes tables (`Data/Migrations`). `dotnet ef database update` runs them. Our second migration,
  `AppendOnlyAndIndexes`, uses raw SQL for things EF cannot express (triggers, partial unique indexes), with a `Down()` that undoes it.
- **SaveChangesInterceptor.** `Services/AppendOnlyInterceptor.cs` runs just before EF writes and throws if code tries to
  update or delete a history row. A PostgreSQL trigger does the same inside the database, so even raw SQL is refused.
- **Identity and cookies.** ASP.NET Core Identity stores users and password hashes (`UserManager`) and signs people in
  (`SignInManager`). After login the browser holds an HttpOnly cookie; JavaScript cannot read it, which is safer than
  tokens in `localStorage`. `Authorization/ImsClaimsPrincipalFactory.cs` puts the role into the cookie as a claim.
- **Policies.** `Authorization/Capabilities.cs` is the only place that says which role may do what. `Program.cs`
  turns each capability into an authorization *policy*; controllers say `[Authorize(Policy = Capabilities.OpsReceive)]`.
  The React app never copies this table: it just asks `GET /api/auth/me` for the list.
- **Antiforgery (CSRF).** A malicious site could make your browser send requests with your cookie. So every POST/PUT/DELETE
  must also carry a secret token in the `X-XSRF-TOKEN` header; the site's own JavaScript can read it, other sites cannot.
  See `Controllers/AuthController.cs` (`csrf`) and `Infrastructure/ValidateAntiforgeryFilter.cs`.
- **ProblemDetails.** Standard JSON error format (RFC 7807). `Infrastructure/ApiExceptionHandler.cs` is the one place that
  converts our `DomainException` into HTTP 422 and `DuplicateScanException` into 409.
- **user-secrets.** `dotnet user-secrets set` stores the connection string in your Windows profile, outside the project,
  so the password can never be committed. `Program.cs` throws a helpful message if it is missing.
- **xUnit + WebApplicationFactory.** `tests/Ims.Tests/ImsApiFactory.cs` starts the real API in memory against a real
  `ims_test` database. `TestAuthHandler` lets a test pretend to be any role with a header.

### React

- **Vite and components.** Vite compiles the TypeScript/React app; `npm run dev` serves it with hot reload. A component is a
  function that returns markup (JSX), e.g. `pages/HomePage.tsx`. State and data come from *hooks* (`useState`, `useQuery`).
- **The /api proxy.** `client/vite.config.ts` forwards `/api/...` from port 5173 to the API on 5080, so the browser sees one
  site: cookies work and no CORS settings are needed. `build.outDir` writes the finished app into the API's `wwwroot`.
- **React Router.** `App.tsx` lists every route. `RequireAuth` and `RequireCapability` (in `src/auth/`) wrap pages and redirect
  to `/login` or `/forbidden`. List state such as the page number lives in the URL (`useSearchParams` in `HistoryPage.tsx`).
- **TanStack Query.** `useQuery({ queryKey: ['history', { page }], queryFn: ... })` fetches data, caches it under that
  key and re-fetches when the key changes. The key is the cache label.
- **Context.** `auth/auth-context.ts` and `AuthProvider.tsx` make the signed-in user available to any component via `useAuth()`.
- **Forms.** `pages/LoginPage.tsx` uses react-hook-form (tracks inputs) with a zod schema (the rules). The server validates again;
  it is the authority.
- **api/client.ts.** The only file that calls `fetch`. It sends cookies, adds the antiforgery header on POSTs, and turns an error
  body into an `ApiError`.
- **Vitest.** `*.test.ts(x)` files run in a fake browser (jsdom). `RequireCapability.test.tsx` renders the guard inside a fake
  router and checks the redirect.

## Day 2 - 22 Nov 2026: admin panel and account security

### .NET

- **DTOs vs entities.** Entities (`Domain/MasterData.cs`) mirror database tables; DTOs (`Contracts/AdminDtos.cs`) are the JSON shapes
  we promise to clients. We never send an entity, so a new column (like a password hash) can never leak by accident.
- **Model binding + DataAnnotations.** In `MasterDataControllers.cs` a parameter such as `MasterDataRequest request` is filled from the
  JSON body, and `[Required]`/`[StringLength]` on the record are checked by `[ApiController]` before the action runs; a bad body is
  answered with 400 and a field-error list. `[FromQuery] ListQuery query` does the same for `?q=&page=`.
- **One list contract.** `Infrastructure/ListQuery.cs` holds the common query parameters and `PagedResult<T>.CreateAsync` runs
  COUNT + OFFSET/LIMIT. Every list (products, users, ...) reuses it, so paging is written once.
- **Generics.** `MasterDataController<T>` and `MasterDataService.ListAsync<T>` work for any table that inherits `MasterDataEntity`;
  the three concrete controllers only add a URL.
- **ProblemDetails.** Business-rule errors are thrown as `DomainException` and `Infrastructure/ApiExceptionHandler.cs` turns them
  into one standard JSON error (RFC 7807) with an optional `errors` map that React puts under the right input.
- **UserManager and security stamps.** `Services/UserAdminService.cs` creates users, hashes passwords and validates them via
  `UserManager`. The *security stamp* is a random value stored in the user row and in the sign-in cookie; changing it
  (`UpdateSecurityStampAsync`) makes every old cookie invalid. With `ValidationInterval = Zero` (`Program.cs`) the stamp is checked on
  every request, so a deactivated user gets 401 immediately.
- **EF Core translations.** `EF.Functions.ILike` gives case-insensitive search in PostgreSQL; `x.Name.ToLower() == lower` becomes
  `lower(name) = ...`, matching the partial unique index from Day 1.
- **Migrations.** `dotnet ef migrations add AddUserLastLogin` generated `Data/Migrations/*_AddUserLastLogin.cs` from the change to
  `ApplicationUser`.

### React

- **Controlled forms.** `pages/manage/MasterDataForm.tsx` uses react-hook-form (`register`, `handleSubmit`, `formState.errors`) with a
  zod schema as resolver. `lib/serverErrors.ts` copies the server's field errors into the form with `setError`.
- **useMutation + invalidateQueries.** In `MasterDataPage.tsx`, `useMutation` sends the change; on success
  `queryClient.invalidateQueries({ queryKey: ['master', kind] })` marks the cached lists out of date so they reload.
- **URL search params as state.** `hooks/useListParams.ts` reads and writes `?q=&page=&status=` through React Router, so Back, refresh
  and copied links keep the list exactly as it was.
- **Reusable components.** `DataTable`, `ListToolbar`, `Pagination`, `ConfirmDialog` and `Modal` in `src/components/` are used by every
  admin list; a page only describes its columns.
- **A global 401 handler.** `api/client.ts` calls the handler registered by `AuthProvider`; `Layout.tsx` also asks `/api/auth/me` on
  every page change, so a deactivated user lands on the login page with "Your session has ended".

## Day 3 - 22 Nov 2026 (operations: track, receive, deliver)

### .NET

- **Service layer and DI lifetimes.** All inventory rules live in `Services/InventoryService.cs`; controllers (`Controllers/OpsControllers.cs`)
  only check the request shape and call it. `Program.cs` registers it with `AddScoped` (one instance per HTTP request, sharing that request's
  `DbContext`) and `IClock` with `AddSingleton` (stateless, one for the whole app).
- **async/await with EF Core.** Every database call is `await`ed (`ToListAsync`, `SaveChangesAsync`); the thread is free while PostgreSQL works.
  `CancellationToken ct` is passed along so a cancelled request stops its queries.
- **LINQ.** `EffectiveRows()` is a query built from `Where`, `Any` and `Select` that EF translates to SQL; `ToDictionary` then works on
  the loaded rows in memory (for example `taken` in `TrackAsync`).
- **Transactions and locking.** `BeginTransactionAsync` groups the checks and inserts; leaving the method without `CommitAsync` rolls back.
  `FromSql($"SELECT ... FOR UPDATE")` locks the unit row so two scans of one barcode run one after the other. `FromSql` with `$"..."`
  sends the values as SQL parameters, never as pasted text.
- **Custom exceptions to ProblemDetails.** `DomainException` (422) and `DuplicateScanException` (409) are thrown by the service;
  `Infrastructure/ApiExceptionHandler.cs` is the only place that turns them into JSON, so the rules never mention HTTP.
- **Database trigger.** `Data/Migrations/*_TransactionChecks.cs` creates a PL/pgSQL function and a `BEFORE INSERT` trigger as a second line of
  defence. A `CASE` is computed into a variable first because it cannot sit inside an `IF` condition.
- **Model binding.** `HistoryListQuery` inherits `ListQuery` and adds filters; `[FromQuery]` fills them from `?eventType=&from=...` by name.

### React

- **Custom hooks.** `hooks/useUnitLookup.ts`, `useOptions.ts`, `useRememberedPoint.ts` and `pages/ops/useOpsPage.ts` hold logic that several
  pages share, so a page only describes its screen.
- **useFieldArray.** `pages/ops/TrackPage.tsx` keeps the list of scanned components inside the react-hook-form state: `append` adds a row,
  `remove` deletes one, `errors.components[i]` carries each row's error. `lib/serverErrors.ts` turns the server key `components[2].componentBarcode`
  into the form path `components.2.componentBarcode`.
- **Refs and focus.** `codeInput.current?.focus()` returns the cursor to the component barcode box after each add. For "scan next" the page
  changes the `key` of `ScanPanel`; React then builds a new panel, and `autoFocus` focuses it.
- **Query invalidation after mutations.** `useOpsPage.finish` calls `invalidateQueries` for `['unit']`, `['history']` and `['dashboard']`, so
  the unit card, history and counts reload after a save. The query key `['unit', barcode]` is shared by `ScanPanel` and the page, so there is
  only one request.
- **State in the URL.** `HistoryPage.tsx` keeps all filters in the query string with `useListParams`, so a filtered history can be bookmarked.
- **Testing UI.** `ScanPanel.test.tsx` replaces `fetch` with a fake (`vi.stubGlobal`) and types into the input like a user; `lib/batch.test.ts`
  tests the duplicate rule as a plain function.

## Day 4 - 23 Nov 2026 (reversals, reports, export)

**.NET**
- **LINQ projection into a record.** `Services/HistoryQueries.cs` turns history rows into `HistoryRowDto` with `.Select(t => new HistoryRowDto(...))`. EF Core
  translates it into one SQL query, including the small sub-queries that find "the row that reversed this one". Selecting only the columns you need is cheaper than loading whole entities.
- **GroupBy in memory.** `Services/ReportService.cs` loads the effective rows once (`ToListAsync`), then uses `GroupBy` per unit to find the last RECEIVED/DELIVERED row.
  When the data is small this is simpler than one giant SQL statement, and the rule stays readable in C#.
- **Records as DTOs.** `Contracts/ReportDtos.cs` describes every report answer as a one-line `record`. A record is an immutable data holder; `row with { CanReverse = true }` copies it with one change.
- **A pure function for rules.** `InventoryService.EvaluateReversal` takes data and returns a verdict without touching the database. The preview, the list buttons and the real reversal all call it, and it is easy to reason about.
- **Returning files.** `Services/ExportService.cs` builds the bytes (CSV text or an Excel workbook from ClosedXML) and returns `FileContentResult` with a content type and a file name; the browser downloads it. Controllers return it from the same action as the JSON list when `?format=` is present.
- **Exception to HTTP.** `NotFoundException` is mapped to 404 in `Infrastructure/ApiExceptionHandler.cs`, next to the 422 and 409 mappings, so services just `throw`.
- **Transactions + row lock.** `ReverseAsync` opens a transaction, locks the unit row (`SELECT ... FOR UPDATE`) and **re-checks** the rule after the lock, so two admins cannot reverse the same row.

**React**
- **Charts with react-chartjs-2.** `pages/reports/SummaryChart.tsx` registers the Chart.js pieces it needs and renders `<Bar data options />`. A canvas cannot read CSS `var(--bs-success)`, so the colour is read with `getComputedStyle`.
- **Derived UI state.** The summary inputs show `f.from || report.data?.from`: the URL holds only what the user chose, the server's default fills in the rest. Nothing is copied into `useState`.
- **Forms with zod.** `pages/history/ReverseForm.tsx` uses `zodResolver(reverseSchema)`; a short reason is blocked before any request. A server error is shown with `applyServerErrors`.
- **Refetch vs optimistic update.** After a reversal `ReversePage.tsx` calls `invalidateQueries` for history, units, dashboard and `['reports']`: React Query reloads the truth from the server. We do not guess the new state locally because the rules live on the server.
- **Plain links for downloads.** `components/ExportButtons.tsx` renders `<a href download>`; `exportUrl()` is a pure function, so a test pins the URL.
- **Search params as input.** `pages/ops/useOpsPage.ts` reads `?barcode=` once with `useSearchParams` and gives it to `ScanPanel` as its starting text.

## Day 5 - 23 Nov 2026 (audit log, dashboard, demo data, SQL)

**.NET**
- **EF Core interceptors and the ChangeTracker.** `Services/AuditInterceptor.cs` is a `SaveChangesInterceptor`: EF calls it just before and just after saving. Before saving it walks `context.ChangeTracker.Entries()` (every entity that is Added, Modified or Deleted), compares `OriginalValue` with `CurrentValue` and adds `AuditLog` rows to the same save. A new row's key does not exist before the INSERT, so CREATE rows are added in `SavedChanges`.
- **IHttpContextAccessor.** The interceptor is created once for the whole app, so it cannot receive the current request in its constructor. `IHttpContextAccessor` (registered with `AddHttpContextAccessor`) gives it `HttpContext.User` (who) and `Connection.RemoteIpAddress` (from where). It is null for the seeder, which is why that row says "system".
- **System.Text.Json.** Old and new values are collected in a `Dictionary<string, object?>`, turned into text with `JsonSerializer.Serialize` and stored in a PostgreSQL `jsonb` column; `AuditQueryService` reads them back with `JsonSerializer.Deserialize<JsonElement>` so the API sends real JSON, not a string.
- **Whitelists beat blacklists.** Listing the few properties we WANT to log is safer than listing the secret ones we must hide.
- **Migration scripts.** `dotnet ef migrations script --idempotent` writes all migrations as one SQL file where every step first checks `__EFMigrationsHistory`, so it can run on an empty or an already migrated database. `pg_dump` is the other half: structure plus data.
- **Seeding through the real service.** `Data/Seed/DemoDataSeeder.cs` builds an `InventoryService` by hand with a clock we can set (`DemoClock`), so old dates go through the same rules as live scans. `new Random(42)` makes the "random" choices the same every run.
- **Environment variables.** `Program.cs` reads `IMS_DATABASE` so a script can aim the app at another database name without touching the secret.

**React**
- **Composing dashboard cards.** `pages/HomePage.tsx` has one small `StatCard` component used six times with different props, and plain Bootstrap tables for the lists. All numbers come from one query (`['dashboard']`).
- **Conditional rendering by capability.** Buttons come from a list filtered with `can(user, capability)`; the recent-activity card only renders when the server sent rows; a barcode is a `<Link>` only for roles that may open reports. The client never re-encodes the permission matrix.
- **Expandable table rows.** `pages/AuditPage.tsx` keeps a `Set` of open ids in state and renders an extra `<tr>` under an open row; the old/new values load lazily with a second query only when a row is opened.
