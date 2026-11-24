# Progress log

## Day 1 — 22 Nov 2026
- **Works:** React + ASP.NET Core skeleton end-to-end. Full PostgreSQL schema via EF migrations (with append-only triggers),
  Identity cookie login + antiforgery + server-side role policies, role-aware menu, every route reachable (stubs say which day),
  the exact sample record seeded, `/history` and dashboard numbers real.
- **Tests:** 16 .NET tests (policy matrix over every endpoint x 5 roles, real login, append-only, seed twice) and 12 Vitest tests, all passing.
- **Checked in the browser:** all five users sign in, each sees only its menu, refresh keeps the session, viewer is shown Forbidden on `/ops/receive` and gets 403 from the API; demo mode on :5080 works.
- **Next:** Day 2 — admin panel (master-data CRUD with soft delete, user management, password change, forced logout).
- **Risk:** Days 3 and 4 are the heaviest (every feature needs an API endpoint and a React screen).

## Day 2 - 22 Nov 2026
- **Works:** admin panel for products, components, points (search, filters, paging, create/edit, delete with soft-delete archive + restore) and users (create with one role, edit, activate/deactivate, reset password, pending-registration filter), password change for every role, forced logout of deactivated users, last-admin and self-deactivation protection.
- **Tests:** 40 .NET tests (24 new: CRUD, case-insensitive duplicates, archive vs hard delete, options, user rules, real-cookie forced logout, password change/reset, 403 for non-admins) and 21 Vitest tests, all passing.
- **Checked in the browser:** the products screen and the delete dialog; deleting Refrigerator archives it with the explanatory message (restored afterwards). The two-window forced logout is covered by an API test with real cookies, not by a manual two-browser run.
- **Next:** Day 3 - track / receive / deliver with the inventory rules, duplicate-scan warning and DB safety triggers.
- **Risk:** audit logging of admin actions is Day 5 (carry-over); Day 3 is heavy.

## Day 3 - 22 Nov 2026
- **Works:** Track (new unit registers with a product, batch of components, all-or-nothing), Receive, Deliver, the duplicate-scan warning (409: point, time, user), clear 422 errors, a database trigger as a safety net, and `/history` with filters (event, product, point, user, date range), search and paging.
- **Tests:** 64 .NET tests (24 new: every rule R2-R5, batch atomicity, trigger via raw SQL, archived data, 403s, lookup shape, history filters) and 29 Vitest tests, all passing.
- **Checked in the browser (as admin):** a new barcode in Track shows the amber card and product select; scanning the same component twice shows the yellow warning; saving a component owned by another unit shows the error on its row and in the alert; History filtered by DELIVERED. The full tracker -> receiver -> deliverer walk with the three role accounts was covered by API tests, not clicked through.
- **Next:** Day 4 - reversals, the three reports with a chart, CSV/Excel export.
- **Risk:** Day 4 is heavy (reversal rules plus three reports).

## Day 4 - 23 Nov 2026
- **Works:** admin reversals with live preview and reason (detach component, back to Registered, back in stock at the original receive point), struck-through reversed rows, three reports (stock with per-point chips, summary with totals + bar chart + table, barcode history timeline by unit or component barcode), and CSV/Excel export on history, reports and the admin lists.
- **Tests:** 98 .NET tests (34 new: every reversal rule allowed/blocked, 403s, stock/summary/barcode numbers, CSV/XLSX content, filters, paging ignored) and 36 Vitest tests (7 new: export URLs, short-reason form), all passing.
- **Checked in the running app (as admin):** reversed the delivery of a demo unit -> back in stock at its receive point, the success link opens /ops/deliver with the barcode pre-filled; summary 27-30 March shows 1/1/1 with a rendered chart; the Excel download is served with the right content type. Not clicked through as the viewer; viewer rights are covered by API tests.
- **Next:** Day 5 - audit log, dashboard tiles, demo data, final diagrams, SQL deliverables.
- **Risk:** the stock report is computed in memory (fine for hundreds of units). The sample record was not reversed in `ims`; a separate demo unit DEMO-UNIT-0001 was used and is still in that database.

## Day 5 - 23 Nov 2026
- **Works:** audit log (master data + users with old/new values, login / failed login / logout, no password data), the `/audit` screen with side-by-side values, filters and export; a real dashboard (today's counts, stock by receive point, stuck-longest list, recent activity, quick actions per role); `seed --demo-data` (25 units, 3 reversals, 1 archived component); final ER and process-flow diagrams; `docs/db/schema.sql`.
- **Tests:** 114 .NET tests (16 new: audit rows and values, soft delete/role/deactivate, password events, login events, append-only, 403s, filters, export, dashboard metrics, sample record end to end) and 39 Vitest tests (3 new: dashboard cards and role-based quick actions), all passing.
- **Checked in the running app (admin):** demo data produced a dashboard with 19 in stock and a stuck list led by DEMO000005 (27 days); renaming a product showed an UPDATE row with old and new name under /audit.
- **Not done:** `docs/db/inventory_with_sample_data.sql` - the script that makes it reads the database password, which the assistant was not allowed to run. Run `powershell -ExecutionPolicy Bypass -File scripts/export-sql.ps1 -Verify` once (API stopped), then commit the file.
- **Next:** Day 6 - QA, test-case sheet, user guide with screenshots, diagram images, demo script and slides, v1.0.0.
- **Risk:** the diagrams are Mermaid text (render check not done in a Mermaid viewer); demo data now lives in the `ims` database.

## Day 6 - 23 Nov 2026 (QA and submission)
- **Done:** 7 Playwright specs (sample flow through the UI, 5-role x 15-route UI matrix, 35 screenshots of every module), diagram PNG/PDF rendered, 97-case executed test sheet (.csv + .xlsx via `tools/Ims.DocTools`), user guide with all screenshots, final README, demo script + Marp slides, `docs/SUBMISSION.md`.
- **Tests:** `dotnet build -warnaserror` clean; 114 .NET, 39 Vitest, 7 Playwright all passing; test sheet 96 Pass / 0 Fail / 1 Not run.
- **Not done / for you:** open an export in desktop Excel (case TC-097); rehearse the demo once with a timer; the `ims_fresh` database variant of the fresh-clone check (needs the DB password, see DECISIONS.md).
- **Tags:** day-6 and v1.0.0.

## v1.0.1 (24 Nov 2026)
- **Fix:** login failed with "Invalid antiforgery token" when the browser kept a stale XSRF-TOKEN cookie (e.g. after a database reset). The API client now fetches a fresh token and retries the request once (`client/src/api/client.ts`); 2 Vitest regression tests added (41 total).
