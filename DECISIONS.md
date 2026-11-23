# Decisions

Every judgment call: date, decision, reason. The **Carry-over** section at the bottom lists work pushed to a later day.

## Assumptions A1-A15 (from the plan, 19 Nov 2026)

- **A1. Unit barcodes.** A product barcode identifies one physical unit (a serial number), not a SKU. The first scan of an unknown barcode, at tracking or receiving, registers the unit and requires choosing its product name. Later scans show the product as read-only.
- **A2. When components can be tracked.** Allowed before receiving and while the unit is in stock; blocked after delivery.
- **A3. When a unit can be received.** Only when it is neither in stock nor delivered. No transfers between receive points. A delivered unit cannot be received again unless its delivery is reversed.
- **A4. Corrections.** A correction is a reversal followed by re-entering the right operation. After a successful reversal the screen links to the operation screen with the barcode pre-filled.
- **A5. Reversal rows.** A reversal is a new row with `event_type = REVERSED` pointing at the row it cancels. A row can be reversed at most once, a reversal can never be reversed, and the reason must be at least 5 characters.
- **A6. Registration.** Self-registration creates a Report Viewer account that stays inactive until an Admin activates it. Admins can also create users directly with any role.
- **A7. Who sees what.** Reports and history: Admin and Report Viewer. Audit log: Admin only. Dashboard: read-only, every signed-in role. Operational roles land on their own scan screen.
- **A8. Duplicate-scan warning.** Client: the same barcode twice in the current form/batch shows an inline warning and is not added again. Server: a scan repeating a state the unit is already in returns a warning (when, where, by whom) and saves nothing. Rule violations are hard errors.
- **A9. Soft delete.** Master data referenced by any unit or history row is soft-deleted (hidden from dropdowns, still shown in history, restorable). Unreferenced rows are hard-deleted after confirmation. Users are never deleted.
- **A10. Scale.** Demo scale (hundreds of units). Status is derived in C# from history rows; no cached status column.
- **A11. Scanners.** A USB/HID scanner acts like a keyboard and sends Enter; manual typing works the same. No camera scanning.
- **A12. Time zone.** Stored as UTC (`timestamptz`), sent as ISO 8601 UTC, shown in the zone from `App:TimeZone` (via `GET /api/config`). Seeded times are read in that zone.
- **A13. Barcode format.** Trimmed, upper-cased, must match `^[A-Z0-9-]{6,32}$`.
- **A14. Database.** PostgreSQL 17 on localhost:5432. The app connects as `ims_user`, which owns `ims` and may create databases (tests use `ims_test`).
- **A15. Architecture.** React SPA (Vite + TypeScript) + ASP.NET Core Web API on .NET 10 LTS, one origin, Identity cookie + antiforgery header, no tokens in localStorage. Not Next.js.

## Day 1 choices (22 Nov 2026)

| Decision | Reason |
|---|---|
| Solution file is **`Ims.sln`** (created with `dotnet new sln --format sln`), not `.slnx` | .NET 10 defaults to `.slnx`; the prompts and docs say `Ims.sln` |
| `App:TimeZone = "Asia/Dhaka"`; seeder falls back to the Windows id `Bangladesh Standard Time` | IANA id works with .NET on this machine; the fallback is cheap insurance |
| `users.role` has **no database default** | ADMIN is the enum's zero value; EF would replace it with the DB default (REPORT_VIEWER) on insert. The app always sets the role explicitly. Found via an EF warning |
| Custom `ValidateAntiforgeryFilter` instead of `[AutoValidateAntiforgeryToken]` | The built-in attribute needs MVC view services that an API-only app does not register (it caused HTTP 500) |
| Explicit `app.UseRouting()` after `UseStaticFiles()`; SPA fallback endpoint is `.AllowAnonymous()` | Otherwise the fallback endpoint wins over real files in `wwwroot`, and deep links like `/ops/receive` hit the sign-in requirement |
| Login checks the password first, then `IsActive` (403 "Account inactive") | Does not reveal whether an inactive account exists to someone without the password. `CanSignInAsync` still refuses inactive users as a backstop |
| Unknown `/api/...` URLs answer 401 when signed out (fallback policy) | Simple and safe; a signed-in user gets 404 |
| Antiforgery token is re-fetched after login and logout | The token is bound to the current identity |
| Client "me" state kept in a React Query entry; on login/logout only *other* cached queries are removed | `queryClient.clear()` detached the watched "me" query and the screen did not update on logout |
| Unknown client route shows the Forbidden page | Placeholder; a proper Not Found page can come later |
| Tests: one shared `ImsApiFactory` for all test classes (xUnit collection), `ims_test` dropped/created once per run | One database, no parallel clashes. Tests reuse the secret's host/password and only swap the database name |
| Policy test walks the real routing table (`EndpointDataSource`) | A new endpoint without a policy can never slip through unnoticed |
| `AuditInterceptor`, `Clock`, `ExportService`, `ReportService`, `UserAdminService` not created yet | They belong to later days; Day 1 only has what it uses |
| Installed NuGet versions pinned exactly in the csproj files | Task asked for exact versions |
| `Microsoft.AspNetCore.OpenApi` template package left in | Harmless; remove later if unused |
| The `check_transaction()` insert-time trigger of the reference SQL is not in the migrations yet | Day 3 adds the DB safety triggers with the operations |

## Day 2 choices (22 Nov 2026)

| Decision | Reason |
|---|---|
| **Soft delete rule:** product in use = referenced by a unit; component / point in use = referenced by a history row. In use -> `is_deleted` + `deleted_at` (response `{archived: true}`); not in use -> real DELETE. Deleting an already archived, in-use row is a no-op that still answers `archived: true` | A9/R11. Keeps history readable and restorable |
| Restore is refused (422 on `name`) if an active row now has the same name; creating a name that only an archived row has is allowed | The unique index only covers non-deleted rows |
| A point type cannot change once the point is used in history (422 on `type`) | Otherwise old history rows would say TRACK at a delivery point |
| One shared `MasterDataService` + one generic `MasterDataController<T>` (3 tiny subclasses) for products, components and points | Same columns and rules; avoids three copies. It is not a generic repository |
| `GET /api/<kind>/options` is protected by `ops.lookup` (not `masterdata.manage`) | Day 3 scan screens of non-admin roles need dropdowns. The Report Viewer still gets 403 |
| `status` list filter defaults to `active`; `deleted` = archived only; `all` | Admin normally wants active rows and switches to Archived to restore |
| New column `users.last_login_at` (migration `AddUserLastLogin`), set on each successful login | "Pending registration" = inactive AND never logged in needs a "never logged in" fact; the audit log (Day 5) cannot serve this yet. Note: an admin-created user who is deactivated before ever signing in also shows as pending |
| **Last-admin protection:** deactivating or changing the role of the only active ADMIN is a 422 (`last-admin`); deactivating yourself is a 422 (`self-deactivate`). Changing your own role is allowed unless you are the last admin | R12 |
| **Zero-interval stamp validation:** `SecurityStampValidatorOptions.ValidationInterval = Zero` (set on Day 1) plus `UpdateSecurityStampAsync` on deactivation: the cookie is re-checked against the database on every request, so the next call is 401. The role claim is also rebuilt each request, so a role change applies at once. Password reset and change also renew the stamp | R12 |
| Admin password reset uses Identity's reset-token flow, so `AddDefaultTokenProviders()` was added | Gets the normal password validators; the old password is never returned |
| The user name cannot be edited after creation | It is the login name; avoids surprises in audit and history |
| Login with an inactive account answers 403 with the title "This account is not active" (was "Account inactive") | Wording required by the Day 2 brief; the password is still checked first |
| `role` is `Role?` + `[Required]` in the create/update user requests | A missing role would otherwise become ADMIN (the enum zero value) |
| Dialogs are our own React modal (`Modal.tsx`), not Bootstrap's JavaScript modal | Fewer moving parts; testable |
| Layout re-checks the session (`GET /api/auth/me`) on every route change | A forced logout shows on the very next click even on screens that load no data |
| `?format=csv` or `xlsx` on lists returns 501 (`NotBuilt(4)`), and the Export buttons show that message | Day 1 rule: stubs never fake success. The real export is Day 4 |
| Policy test now uses a non-existent id (`999999999`) in place of `{id}` | Allowed roles used to really execute DELETE/toggle-active on row 1; now they get 404 and nothing changes |
| Vitest setup now unmounts components after each test | Without globals React Testing Library does not clean up by itself |
| Master-data and user changes are NOT written to `audit_log` yet | `AuditInterceptor` is Day 5 work (the plan puts the audit log there); R13 is completed then |

## Day 3 choices (22 Nov 2026)

| Decision | Reason |
|---|---|
| **Warning vs error:** a scan that repeats the unit's current state (receive an in-stock unit; track a component already on this unit) is a **409 duplicate-scan warning** with when/where/by; every rule violation (component on another unit, delivered unit, wrong status, wrong point type, archived data, bad barcode) is a **422**. Neither saves anything | A8. The user sees "someone already did this" without it looking like a failure |
| Deliver of an already delivered unit is a 422 (not a warning) | The brief asks for "a clear error" |
| **Row locking:** each operation opens a transaction and loads the unit with `SELECT ... FOR UPDATE`. A brand-new barcode has no row to lock, so two simultaneous first scans race on the unique index on `units.barcode`; the loser gets a friendly 422 `concurrent-scan` | Enough for demo scale; avoids advisory locks |
| Validation errors of a Track batch use keys like `components[2].componentBarcode`; the client maps them onto the matching row | The whole batch is validated first, so every bad row is reported at once |
| If a batch has both real errors and a duplicate on this unit, the errors win (422) | Fixing hard errors first is less confusing than a warning |
| `InventoryService` returns the updated unit (`UnitLookupDto`) from every operation | The UI refreshes without another call |
| Added `IClock`/`SystemClock` (`Services/Clock.cs`) | One place for "now" (UTC) |
| Lookup `recentEvents` shows the last 5 rows including reversals; `components` shows only effective ones | History stays honest; the card shows the current state |
| Added `GET /api/history/filter-options` (products, points, users incl. archived) with `history.view` | The Report Viewer may not call `/options` (`ops.lookup`) but needs dropdowns for the history filters |
| History date filters are local days in `App:TimeZone` converted to UTC | A12; the seeded 10:00 Dhaka row is found by `from=to=2026-03-27` |
| Track page keeps the chosen point outside the form (dropdown + sessionStorage) and checks it on submit | Options load after the form is created; avoids syncing state with an effect |
| Pages "scan next" by changing the `key` of `ScanPanel` | Remounting clears the box and `autoFocus` puts the cursor back; no refs or effects |
| Older tests that assumed an empty test database (dashboard counts, history total) now look only at the seeded sample unit | All test classes share `ims_test`, and the new operation tests add units |
| The trigger test inserts one scratch reversal row by raw SQL in `ims_test` | The only way to prove "a reversal cannot be reversed" before Day 4 builds reversals; append-only means it stays, harmlessly |

## Day 4 choices (23 Nov 2026)

| Decision | Reason |
|---|---|
| **Reversal-point semantics:** the REVERSED row records the point of the row it cancels (track point / receive point), except a reversed DELIVERED row, which records the **receive point the unit returns to** (the one of its last effective RECEIVED row) | `inventory_transactions.point_id` is NOT NULL; R9 says the unit returns to its original receive point, and "stock by receive point" is derived from the last effective RECEIVED row anyway |
| Reversing a COMPONENT_TRACKED row is allowed in every status, also after delivery | R10 has no status condition and the brief says only "detach the component" |
| The reversal rules are one pure function (`EvaluateReversal`) used by the preview, the list's `canReverse` flag and the real reversal (which re-checks inside the locked transaction) | Button, preview and action can never disagree |
| `canReverse` is only computed for admins (one small query per distinct unit on the page); everyone else gets `false` | Saves work, and nobody else may reverse |
| A too-short reason is a **422** with a field error on `reason` (the request body only enforces "required, max 500") | Same `code`/field-error shape as every other rule; the React form blocks it first with zod |
| New `NotFoundException` -> 404 ProblemDetails (unknown history row) | Operations errors were 422/409 only; a wrong id is a 404 |
| `AppTime` (singleton) holds the display zone; `HistoryController` no longer has its own copy | One place for "local day" and "local time" (history filters, summary days, exported times) |
| Exports reuse the list's filter code and just skip paging (master data and users got a `BuildListQuery` shared by list and export) | The file always matches the screen |
| Export buttons are plain `<a download>` links, not fetch | Same origin, so the cookie is sent; no blob handling |
| CSV text cells starting with `=`, `+`, `-`, `@` get a leading `'` | Stops Excel running imported text as a formula (CSV injection). Excel cells are always written as text |
| Excel export has one sheet named after the report; times are text in the display zone, numbers are numbers | Simple and readable; no time-zone surprises when opened in Excel |
| **Stock report** is computed in memory from the effective rows of all units (one query), then filtered/sorted/paged | A10 (demo scale); keeps the "derived, never stored" rule in one readable place. The per-point chips ignore the point filter so they stay usable |
| "Days in stock" = whole days (rounded down) since the unit's last effective RECEIVED row | Matches "longest-stuck stock" on the Day 5 dashboard |
| **Summary report:** default range = today and the 29 days before it (30 days, inclusive); every day appears even with zeros; the table is paged (15 days) but the chart gets all days; "Tracked" counts one row per component | Matches the brief's per-day graph; the sample 2026-03-27..30 gives 1/1/1 |
| An invalid summary range is a 422 with field errors on `from` / `to` | Shown under the date inputs |
| **Barcode report:** a unit barcode wins over a component barcode if both exist; a component barcode matches every unit it was **ever** attached to (reversed rows count); several matches -> the user picks; nothing found -> 200 with `found: false` | The report is a full history; "not found" is an answer, not a failure |
| The barcode export needs a single unit (422 otherwise) | An export of "pick one" makes no sense |
| `?barcode=` on the three scan pages is read by `useOpsPage` and handed to `ScanPanel` (`initialBarcode`) | The link after a reversal pre-fills and looks up the unit; only the first panel is pre-filled, "scan next" starts empty |
| Report filter dropdowns reuse `GET /api/history/filter-options` | The Report Viewer may not call `/options` |
| Chart colours are read from Bootstrap's CSS variables at render time | A canvas cannot use `var()`; keeps the chart in the Bootstrap palette |
| An old Day 2 test now filters history to the sample unit instead of assuming every row is a Refrigerator | New tests create Freezer / Air Conditioner units in the shared `ims_test` |
| Manual check used a separate demo unit (`DEMO-UNIT-0001`) and **not** a database reset | The `ims` database may hold the user's own test data; the API tests already prove "reverse a delivery" on throw-away units |

## Carry-over

- Day 6 deliverable: diagrams are Mermaid **drafts** (`docs/diagrams/`); final images come on Day 5/6.
- The README screenshots and user guide are Day 6.
- A proper 404 page for unknown client routes (minor).
- Day 5: write master-data and user changes (old/new values) and login/logout events to `audit_log` (R13), including the Day 2 admin actions.
