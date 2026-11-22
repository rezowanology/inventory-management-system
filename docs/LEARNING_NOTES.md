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
