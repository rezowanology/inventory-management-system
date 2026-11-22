# Progress log

## Day 1 — 22 Nov 2026
- **Works:** React + ASP.NET Core skeleton end-to-end. Full PostgreSQL schema via EF migrations (with append-only triggers),
  Identity cookie login + antiforgery + server-side role policies, role-aware menu, every route reachable (stubs say which day),
  the exact sample record seeded, `/history` and dashboard numbers real.
- **Tests:** 16 .NET tests (policy matrix over every endpoint x 5 roles, real login, append-only, seed twice) and 12 Vitest tests, all passing.
- **Checked in the browser:** all five users sign in, each sees only its menu, refresh keeps the session, viewer is shown Forbidden on `/ops/receive` and gets 403 from the API; demo mode on :5080 works.
- **Next:** Day 2 — admin panel (master-data CRUD with soft delete, user management, password change, forced logout).
- **Risk:** Days 3 and 4 are the heaviest (every feature needs an API endpoint and a React screen).
