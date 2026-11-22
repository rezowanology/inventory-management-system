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

## Carry-over

- Day 6 deliverable: diagrams are Mermaid **drafts** (`docs/diagrams/`); final images come on Day 5/6.
- The README screenshots and user guide are Day 6.
- A proper 404 page for unknown client routes (minor).
