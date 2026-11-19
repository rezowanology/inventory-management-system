(Project context: CLAUDE.md in this folder is loaded automatically and is the authoritative
project specification. Follow it. The full plan is docs/planning/IMS_Build_Plan.md.)

=== DAY 4 OF 6: ADMIN REVERSALS, REPORTS, CSV/EXCEL EXPORT ===

1. ROLE
You are a senior full-stack engineer with a reporting/analytics focus, careful about data
correctness and side effects. Show the intern how LINQ projections, a small export service and a
React chart fit together.

2. CONTEXT & CURRENT STATE
Days 1–3 are done (tags day-1..day-3): append-only history with derived status, Identity cookie +
policies, admin panel (soft delete, activation, forced logout, password change), working
Track/Receive/Deliver with duplicate-scan warnings and a DB safety-net trigger, a filterable
/history, the shared list contract (export still returns 501). The reverse endpoints, the three
report endpoints and their React pages are still stubs.
MUST NOT REGRESS: all operations rules and tests, policy matrix, sample record, admin panel.
Starting-state check:
    git checkout main && git pull ; git tag --list "day-*"  -> day-1..day-3
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Clear any Carry-over items in DECISIONS.md first.

3. TODAY'S OBJECTIVE
Add admin-only reversal/correction with correct side effects, the three history-based reports
(stock details, date-range summary with a chart, barcode-wise history), and CSV/Excel export for
every list and report. Advances: Core 7, Core 8; Additional 1 (reports), 2.

4. ORDERED TASK LIST
 1. InventoryService.ReverseAsync(rowId, reason, admin) per R7–R10 in one locked transaction:
    reason >= 5 chars; target not already reversed and not a REVERSED row; RECEIVED only if the
    unit is not Delivered; DELIVERED only if it is the latest effective delivery (new row's point
    = the receive point the unit returns to); COMPONENT_TRACKED -> detach. Writes one REVERSED
    row. CanReverseAsync(row) -> (allowed, reason) and a preview text.
 2. API: GET /api/history/{id}/reverse-preview (history.reverse) -> {row, allowed, blockedReason,
    effect}; POST /api/history/{id}/reverse {reason}. GET /api/history rows now include
    {reversed, reversedBy, reversedAt, canReverse} (canReverse only computed for admins).
 3. React: on /history (admin only) a Reverse button on rows where canReverse is true (others a
    disabled button with a tooltip); /history/:id/reverse shows the original row, the effect
    preview (e.g. "Unit 2440701AK2057 will return to stock at Refrigerator Receive Point 1") and a
    required reason textarea (zod min 5). On success: a message + "Re-enter the correct entry"
    link to the relevant ops page with ?barcode= pre-filled (ScanPanel reads it). Reversed rows
    are struck through with "reversed by X on Y".
 4. ExportService: ToCsv (RFC 4180, UTF-8 with BOM) and ToXlsx (ClosedXML: bold header, adjusted
    widths, frozen header, auto-filter). Every list endpoint with format=csv|xlsx returns
    File(...) named <report>_<yyyyMMdd-HHmm>.<ext>, using the same filters, no paging, local
    times. React ExportButtons build the same URL from the current list params and trigger the
    download with a normal link (same-origin cookie, no fetch needed). Enable on /history, all
    reports and admin lists.
 5. Reports API (ReportService, reports.view) + React pages:
    - /reports/stock: one row per InStock unit: barcode, product, current receive point, received
      at, received by, days in stock, number of attached components; summary chips per receive
      point; filters product, receive point, minimum days; search barcode; sort by days; paging;
      export.
    - /reports/summary: from/to (default last 30 days; server validates from <= to and range <=
      366 days with field errors); optional point/product filters; totals cards (tracked,
      received, delivered, reversed); a react-chartjs-2 grouped bar chart per day (received vs
      delivered, + tracked) using Bootstrap's CSS variables for colours; per-day table (paged) +
      export.
    - /reports/barcode: input a product barcode OR a component barcode (a component resolves to
      the unit(s) it was ever attached to; if more than one, let the user pick); header card
      (product, status badge, current point, attached components) and a vertical timeline of ALL
      rows in order, including reversed rows (struck through, linked to their reversal) and
      REVERSED rows with reason and admin; export.
 6. REPORT_VIEWER sees everything read-only (no Reverse buttons, no write forms).
 7. Tests (API): each reversal rule (allowed and blocked, incl. reverse receive after delivery ->
    blocked; reverse delivery -> back in stock at the original receive point; reverse component ->
    re-trackable on another unit; double reversal blocked; non-admin -> 403; short reason -> 400/
    422); stock report counts; summary 2026-03-27..2026-03-30 -> 1 tracked, 1 received, 1
    delivered; barcode history for 2440701AK2057 = 3 rows; CSV/XLSX content type, header row and
    filters honoured (open the XLSX with ClosedXML in the test). Vitest: ExportButtons build the
    right URL from list params; the reverse form blocks a short reason.
 8. README "Reports & export"; DECISIONS.md (reversal-point semantics); LEARNING_NOTES.md Day 4
    (LINQ GroupBy/projections, records as DTOs, returning files from controllers; React: charts
    with react-chartjs-2, derived UI state, optimistic vs refetch after mutations).

5. ACCEPTANCE CRITERIA
- [ ] Admin reverses the sample DELIVERED row with a reason -> 2440701AK2057 is InStock at
      "Refrigerator Receive Point 1"; stock report and dashboard show it
- [ ] Trying to reverse its RECEIVED row while it is delivered is blocked with an explanation
- [ ] Reversing a COMPONENT_TRACKED row lets that component barcode be attached to another unit
- [ ] A row cannot be reversed twice; a REVERSED row has no Reverse button; receiver/viewer get
      403 on the reverse endpoint and the Forbidden page on the route
- [ ] Barcode history for 2440701AK2057 shows the full path including the reversal and reason
- [ ] Summary for 2026-03-27..2026-03-30 shows 1/1/1 and a rendered chart
- [ ] CSV and Excel downloads open correctly in Excel and match the on-screen filters
- [ ] All tests pass (earlier + today's)

6. VERIFICATION
    dotnet build && dotnet ef database update --project src/Ims.Api
    dotnet run --project src/Ims.Api -- seed
    dotnet test && npm test --prefix client && npm run build --prefix client   -> green
    run in dev mode and open http://localhost:5173
Manual: as admin reverse the sample delivery, check the stock report, then reset the database
(dotnet ef database drop --force, database update, seed) so the pristine sample record is back.
As viewer, open all three reports and export each one.

7. DEMO NOTE
Show barcode history for 2440701AK2057 (3 rows), reverse its delivery as admin, refresh to show the
4th row (REVERSED, with reason) and the unit back in stock; then download the Excel export.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "feat(reports): admin reversals, stock/summary/barcode reports with chart and CSV/Excel export"
    git push origin main
    git tag -a day-4 -m "Day 4: reversals, reports, export"
    git push origin day-4

9. GUARDRAILS
- Do not break prior features; operations rules and their tests must stay green.
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Reversals ADD rows; they never modify or delete existing rows.
- Every report number comes from history via the services, never from a stored counter.
