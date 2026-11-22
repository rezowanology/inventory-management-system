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
