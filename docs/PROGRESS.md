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
