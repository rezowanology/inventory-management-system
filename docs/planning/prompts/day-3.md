(Project context: CLAUDE.md in this folder is loaded automatically and is the authoritative
project specification. Follow it. The full plan is docs/planning/IMS_Build_Plan.md.)

=== DAY 3 OF 6: OPERATIONS: COMPONENT TRACKING, RECEIVE, DELIVERY ===

1. ROLE
You are a senior full-stack engineer who owns the domain rules. Correctness of the inventory rules
matters more than visuals today. Show the intern how a service layer, transactions and typed
errors keep rules in one place, and how the React scan screens stay fast for a scanner user.

2. CONTEXT & CURRENT STATE
Days 1–2 are done (tags day-1, day-2): schema with append-only history, Identity cookie +
antiforgery + policies, React shell with guards, seed with the sample record, real history and
dashboard counts, a full admin panel (master data with soft delete, users with roles/activation/
forced logout, password change), and the shared list contract (ListQuery/PagedResult, DataTable,
ListToolbar, Pagination, useListParams). The ops endpoints, /api/units/lookup and the three
/ops pages are still stubs. InventoryService has the read methods; TrackAsync, ReceiveAsync,
DeliverAsync are stubs.
MUST NOT REGRESS: policy tests, append-only tests, seed tests, admin panel tests.
Starting-state check:
    git checkout main && git pull ; git tag --list "day-*"  -> day-1, day-2
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Clear any Carry-over items in DECISIONS.md first.

3. TODAY'S OBJECTIVE
Implement the three scan operations end-to-end with every business rule enforced in
InventoryService, scanner-friendly React pages, client + server validation with a duplicate-scan
warning, a database safety-net trigger, and a filterable history page.
Advances: Core 3, 4, 5, 6; Additional 1 (history), 5.

4. ORDERED TASK LIST
 1. InventoryService: implement
    - GetOrRegisterUnitAsync(barcode, productId?, user)
    - TrackAsync(barcode, productId?, trackPointId, IReadOnlyList<(componentId, componentBarcode)>,
      user): R2, R3; validates the whole batch first, then writes all rows or none
    - ReceiveAsync(barcode, productId?, receivePointId, user): R2, R4
    - DeliverAsync(barcode, deliveryPointId, user): R5
    Each: one transaction (BeginTransactionAsync) that first locks the unit row with
    FromSql("SELECT * FROM units WHERE barcode = {0} FOR UPDATE") when it exists; normalized
    barcode; point-type checks; active master data only; occurred_at = UtcNow via Clock.
    DomainException(code, message, field?) for hard errors; DuplicateScanException(when, where,
    by) for repeats; specific messages such as "Component 94A600321852 is already attached to
    unit 2440701AK2057".
 2. Migration "TransactionChecks": PL/pgSQL check_transaction() + BEFORE INSERT trigger on
    inventory_transactions rejecting a wrong point type, a component barcode already attached
    (ignoring reversed rows), and reversing a REVERSED row. IMPORTANT PL/pgSQL detail: compute
    the expected point type into a variable first; a CASE inside an IF condition ends the
    condition at its first THEN.
 3. API: GET /api/units/lookup?barcode= (ops.lookup) -> {barcode, exists, product, status,
    currentPoint, since, components[], recentEvents[5]}; POST /api/ops/track | receive | deliver
    (matching policies) with request DTOs + DataAnnotations; responses return the updated unit
    status so the UI can refresh without another call. 409/422 come from the shared
    ProblemDetails mapping.
 4. React ScanPanel component + useUnitLookup hook: barcode input with autoFocus; Enter (a USB
    scanner sends Enter) triggers the lookup query; uppercase on input; the pattern mirrors
    lib/barcode.ts; a unit card with StatusBadge (Registered grey, InStock green, Delivered blue,
    NotFound amber). After a successful save: success alert, form reset, focus back on the
    barcode input ("scan next"). The last used point is remembered (sessionStorage, wrapped in
    try/catch).
 5. /ops/track: scan the product barcode -> if unknown, a product select appears (required);
    choose the TRACK point; add component rows (component select + component barcode input,
    useFieldArray). Client: a component barcode scanned twice in this batch, or equal to the
    product barcode, shows a yellow inline "Duplicate scan" warning and is not added. Submit ->
    server validates the whole batch; field errors map back onto the matching row.
 6. /ops/receive: scan -> unit card; if unknown, a product select; choose the RECEIVE point ->
    save. 409 -> a warning alert with point/time/user and nothing saved; 422 -> error alert.
 7. /ops/deliver: scan -> unit card; Save enabled only when InStock (the server re-checks);
    choose the DELIVERY point -> save.
 8. /history upgrade (API + UI) with the list contract: columns time (local), barcode, product,
    event badge, point, component name + barcode, user (employee_no + name), reason. Filters:
    event type, point, product, user, date from/to; search across product or component barcode;
    paging. Leave a placeholder for the "reversed" badge (logic tomorrow).
 9. Tests: API/service tests for every rule (first scan registers; product required for a new
    unit; receive InStock -> 409; receive Delivered -> 422; deliver Registered -> 422; deliver
    InStock -> Delivered and stock count decreases; component taken by another unit -> 422;
    component already on this unit -> 409; batch atomicity; wrong point type rejected by the
    service AND by the trigger via raw SQL; archived master data rejected; each ops endpoint 403
    for wrong roles; lookup JSON shape). Vitest: batch duplicate detection; ScanPanel uppercases
    and submits on Enter; 409 renders the warning alert.
10. README "Operations"; DECISIONS.md (warning vs error, row locking); LEARNING_NOTES.md Day 3
    (service layer + DI lifetimes, async/await with EF Core, LINQ, transactions, custom
    exceptions to ProblemDetails; React: custom hooks, useFieldArray, refs and focus, query
    invalidation after mutations).

5. ACCEPTANCE CRITERIA
- [ ] As `tracker`: scan a NEW barcode (e.g. TESTUNIT0001), choose Refrigerator and the track
      point, attach two components -> two history rows appear
- [ ] Scanning 94A600321852 onto TESTUNIT0001 fails: "already attached to unit 2440701AK2057"
- [ ] Scanning the same component twice in one batch shows the client-side duplicate warning
- [ ] As `receiver`: receive TESTUNIT0001 at Receive Point 1 -> InStock; receiving again shows
      the duplicate-scan warning with point/time/user and saves nothing
- [ ] As `deliverer`: deliver TESTUNIT0001 -> Delivered; the dashboard stock count goes back down
- [ ] Delivering 2440701AK2057 (already delivered) or an unknown barcode -> a clear error
- [ ] /history (admin/viewer) filters by event, point, user and date range, searches by barcode,
      and pages
- [ ] Every rule R2–R5 has a passing test; all earlier tests still pass

6. VERIFICATION
    dotnet build && dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed
    dotnet test --filter "FullyQualifiedName~Inventory"    -> green
    dotnet test && npm test --prefix client && npm run build --prefix client   -> green
    run in dev mode and open http://localhost:5173
Manual: run the acceptance flow with the three ops users in the order track -> receive ->
deliver, then check /history as viewer.

7. DEMO NOTE
Run a brand-new unit through Track -> Receive -> Deliver live with the three role accounts, and
trigger one duplicate-scan warning on purpose.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "feat(inventory): component tracking, receive and delivery with validation and duplicate-scan warnings"
    git push origin main
    git tag -a day-3 -m "Day 3: core operations"
    git push origin day-3

9. GUARDRAILS
- Do not break prior features (admin panel, policies, seed).
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Never add a status column or edit history rows: status is always derived in InventoryService.
- Business rules live only in InventoryService (plus the DB safety net); React only mirrors them.
