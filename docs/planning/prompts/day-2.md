(Project context: CLAUDE.md in this folder is loaded automatically and is the authoritative
project specification. Follow it. The full plan is docs/planning/IMS_Build_Plan.md.)

=== DAY 2 OF 6: ADMIN PANEL, USER MANAGEMENT, ACCOUNT SECURITY ===

1. ROLE
You are a senior full-stack engineer focused on admin tooling and account security with ASP.NET
Core Identity and React forms. Explain Identity, controller and React form idioms briefly for a
learner.

2. CONTEXT & CURRENT STATE
Day 1 is done and tagged `day-1`: Ims.Api (controllers, Identity cookie + antiforgery, policies
from Capabilities.cs, full schema with append-only triggers, seed with the sample record, real
GET /api/history and /api/dashboard, every other endpoint a 501 stub) and the React client
(Vite, router, AuthProvider + guards, layout from me.capabilities, real /history and dashboard,
every other route stubbed), API and Vitest tests, README, DECISIONS.md, LEARNING_NOTES.md, draft
diagrams.
MUST NOT REGRESS: policy matrix tests, append-only guarantees, the sample record, cookie login,
role-based landing pages, demo mode build.
Starting-state check (do this first):
    git checkout main && git pull ; git tag --list "day-*"  -> day-1
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Then read DECISIONS.md and finish any Carry-over items before starting new work.

3. TODAY'S OBJECTIVE
Replace the admin stubs with a working admin panel for master data and users (API + React),
including soft delete, activation, password change and forced logout, on top of a shared list
contract. Advances: Core 1 (complete), Core 2; Additional 1 (admin lists), 4, 5 (admin forms), 7.

4. ORDERED TASK LIST
 1. Shared list contract. Server: ListQuery (q, page, pageSize = 20, sort; bound with
    [FromQuery]) and PagedResult<T>.CreateAsync(IQueryable, ...). Client: components DataTable
    (columns config), ListToolbar (search box + filter selects + Export buttons slot), Pagination
    ("Showing X–Y of Z"), and a useListParams hook that keeps q/page/filters in the URL search
    params. Export buttons render but the API returns 501 until Day 4.
 2. Master data API (Products, Components, Points controllers, masterdata.manage): list (search;
    filter active/deleted/all; Points also by type), get, create, update, delete, restore.
    Request DTOs with DataAnnotations ([Required], [StringLength(100, MinimumLength = 2)]); trim;
    duplicate name -> 422 with a field error (service check first; the DB index is the safety net).
    Delete: referenced by units or history -> soft delete and return {archived: true}; otherwise
    hard delete. A GET .../options endpoint (or ?activeOnly=true) feeds every dropdown with
    non-deleted rows only.
 3. Master data UI (/manage/products, /manage/components, /manage/points): list with search,
    filters and paging; create/edit form (react-hook-form + zod mirroring the server rules; server
    field errors copied with setError); delete with ConfirmDialog, then "In use: archived instead
    of deleted" or "Deleted"; Restore button on archived rows. Invalidate the list query after
    each mutation.
 4. Users API (UsersController, users.manage) via UserAdminService using UserManager: list
    (search username/name/employee_no; filter role, active, "pending registration" = inactive +
    never logged in), get, create (username, names, employee_no unique, one role, initial
    password with Identity validators), update (names, employee_no, role), toggle-active (block
    self and the last active ADMIN with 422 + clear messages), reset-password (admin sets a new
    one; the old one is never returned).
 5. Forced logout: deactivation calls UpdateSecurityStampAsync; with ValidationInterval = Zero the
    user's next API call returns 401; the React client clears the user and shows /login with
    "Your session has ended. Please sign in again." Inactive login attempts show "This account is
    not active".
 6. Password change: POST /api/auth/change-password (old, new x2) with ChangePasswordAsync then
    RefreshSignInAsync; React /change-password page for every role, with success message.
 7. Users UI (/manage/users): list, create/edit forms with a single role select, activate/
    deactivate with ConfirmDialog, reset-password dialog, a "Pending registration" filter chip.
 8. Tests (API): master data CRUD + case-insensitive uniqueness; soft delete when referenced vs hard
    delete when not; dropdown options exclude archived rows; user create/edit/role change;
    self-deactivate and last-admin blocked; a deactivated user's existing cookie gets 401 on the
    next call (real cookie flow); password change works and the old password stops working; all
    admin endpoints 403 for non-admin roles. Vitest: useListParams round-trips URL params;
    server field errors land on the right form fields.
 9. README "Admin panel"; DECISIONS.md (soft-delete rules, last-admin protection, zero-interval
    stamp validation); LEARNING_NOTES.md Day 2 (DTOs vs entities, model binding + DataAnnotations,
    UserManager and security stamps, ProblemDetails; React: controlled forms with react-hook-form,
    zod schemas, useMutation + invalidateQueries, URL search params as state).

5. ACCEPTANCE CRITERIA
- [ ] Admin can create, edit, search, filter and page products, components and points
- [ ] Deleting "Refrigerator" (used by the sample unit) archives it; it disappears from dropdowns
      but /history still shows it; Restore brings it back
- [ ] Deleting a brand-new unused product removes it after a confirm dialog
- [ ] Duplicate names (e.g. " compressor ") show a clear error on the name field
- [ ] Admin can create a user with any one role and change the role later
- [ ] Deactivating `receiver` while receiver is logged in elsewhere sends them to the login page
      with "Your session has ended" on their next action; they cannot log in until reactivated
- [ ] A self-registered user appears under "pending registration" and can log in after activation
- [ ] Admin cannot deactivate self or the last active admin
- [ ] Every role can change their own password
- [ ] Back/forward and copied URLs keep list filters and page
- [ ] All Day 1 tests and today's new tests pass

6. VERIFICATION
    dotnet build && dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed
    dotnet test && npm test --prefix client && npm run build --prefix client   -> green
    run in dev mode (two terminals) and open http://localhost:5173
Manual: open two browsers (normal + private window). Log in as admin in one and receiver in the
other. Deactivate receiver as admin, then click anything in receiver's window -> login page with
the session-ended message.

7. DEMO NOTE
Live forced logout: deactivate `receiver` in one window and show the other window being signed
out. Then archive "Refrigerator" and show it gone from the dropdown but still in history.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "feat(admin): master data CRUD with soft delete, user management, password change and forced logout"
    git push origin main
    git tag -a day-2 -m "Day 2: admin panel and account security"
    git push origin day-2

9. GUARDRAILS
- Do not break prior features: the policy matrix tests must stay green, and the Day 1 stubs for
  ops and reports must stay reachable and guarded.
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Never hard-delete users. Never touch inventory_transactions rows.
- Use the shared list contract and components; no per-page paging code.
