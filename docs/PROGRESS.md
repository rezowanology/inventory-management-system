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

