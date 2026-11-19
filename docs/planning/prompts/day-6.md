(Project context: CLAUDE.md in this folder is loaded automatically and is the authoritative
project specification. Follow it. The full plan is docs/planning/IMS_Build_Plan.md.)

=== DAY 6 OF 6: QA, TEST-CASE SHEET, USER GUIDE, DEMO, SUBMISSION ===

1. ROLE
You are a QA engineer and technical writer (with a full-stack developer's ability to fix
defects). Your job is to prove the system works for every role and every rule, document it for a
first-time user, and prepare a 10-minute demo. No new features today.

2. CONTEXT & CURRENT STATE
Days 1–5 are done (tags day-1..day-5): feature complete. React client + ASP.NET Core API with
Identity cookie + policies for 5 roles; admin panel; Track/Receive/Deliver with duplicate-scan
warnings; admin reversals; stock, summary (+chart) and barcode reports; CSV/Excel export; audit
log; dashboard; seed (--demo-data); final Mermaid diagrams; docs/db/schema.sql + data dump; xUnit
and Vitest tests.
MUST NOT REGRESS: anything. Bug fixes only, each with a test.
Starting-state check:
    git checkout main && git pull ; git tag --list "day-*"  -> day-1..day-5
    dotnet build && dotnet ef database update --project src/Ims.Api && dotnet test
    npm ci --prefix client && npm test --prefix client && npm run build --prefix client  -> green
Clear any Carry-over items in DECISIONS.md first; they are blockers for submission.

3. TODAY'S OBJECTIVE
Produce every remaining PDF deliverable: an executed test-case sheet, a user guide with
screenshots of every module, the final README, diagram images, a demo script + slides, and a
submission checklist, then tag v1.0.0. Advances: all Deliverables; verifies every requirement.

4. ORDERED TASK LIST
 1. Fresh-clone check: clone the repo into a temporary folder and follow README setup EXACTLY
    (user-secrets with Database=ims_fresh so the main database is untouched, npm ci, database
    update, seed, both run modes, tests). Fix any step that fails or is missing.
 2. Playwright (Node, dev dependency in client: @playwright/test; `npx playwright install
    chromium`), scripts in client/e2e/:
    - screenshots.spec.ts: against demo mode (http://localhost:5080, seeded with --demo-data),
      logs in as each role and saves full-page PNGs of every module to docs/user-guide/img/ with
      stable names (01-login.png, 02-dashboard-admin.png, 03-track.png, ..., incl. the Forbidden
      page, a duplicate-scan warning, a reversal confirm, the summary chart, export buttons, the
      audit detail)
    - smoke.spec.ts: one end-to-end run of the sample flow (track -> receive -> deliver ->
      reverse) through the real UI
    - render-diagrams.ts: renders the two Mermaid files (Mermaid from a pinned CDN URL) to
      docs/diagrams/process-flow.png/.pdf and er-diagram.png/.pdf; if offline, record a manual
      mermaid.live export in DECISIONS.md
 3. tools/Ims.DocTools (console, ClosedXML): docs/testing/test-cases.csv -> test-cases.xlsx
    (coloured Pass/Fail, frozen header, filters).
 4. Test-case sheet: docs/testing/test-cases.csv with columns ID, Area, Requirement, Role,
    Preconditions, Steps, Expected result, Actual result, Status (Pass/Fail), Automated test (test
    name or "manual"); at least 45 cases: the full policy matrix (every role x every capability,
    API and UI), rules R1–R13, each additional requirement, the sample record end-to-end, and
    negative cases (bad barcode format, archived master data, invalid date range, short reversal
    reason, missing antiforgery header). EXECUTE every case: automated via dotnet test, npm test
    and the Playwright smoke test; manual ones in the running app. Fill in actual results
    honestly. Each failure: fix with a regression test (separate `fix:` commits), re-run, update.
    Then build the .xlsx.
 5. docs/user-guide/USER_GUIDE.md: intro, sign in / register / change password, then one section
    per role with step-by-step screenshots; every module covered; duplicate-scan and error
    messages explained; FAQ/troubleshooting (forgot password -> ask admin, "account not active",
    "session has ended", what Forbidden means); glossary (unit, component, receive point,
    delivery point, reversal).
 6. README.md final: overview + feature list mapped to the brief, architecture diagram (React ->
    API -> PostgreSQL), stack + rationale (≤5 lines), prerequisites (.NET 10 SDK, dotnet-ef, Node
    LTS, PostgreSQL 17, Git), database setup (ims_user, ims, user-secrets command), dev and demo
    run modes, seed (with/without demo data), reset, tests (dotnet test, npm test, Playwright),
    default admin login + one login per role, project structure, docs index, known limitations
    (demo scale, local PostgreSQL, no camera scanning), link to DECISIONS.md.
 7. Demo kit:
    - docs/demo/DEMO_SCRIPT.md: a timed 10-minute script in DEMO MODE (one process on :5080):
      0:00 intro + stack (slides); 1:00 roles + Forbidden page + API 403; 2:00 admin: master
      data, soft delete, user activation + forced logout; 3:30 sample record barcode history
      (3 rows); 4:30 a new unit live: track (+ duplicate-scan warning) -> receive -> deliver with 3
      accounts; 6:30 admin reversal + barcode history showing it; 7:30 reports: stock, summary
      chart, Excel export; 8:30 dashboard + audit log; 9:15 diagrams + test sheet; 9:45 Q&A.
      Exact barcodes and a pre-demo checklist (PostgreSQL running, reset DB, seed --demo-data,
      `npm run build`, two browser windows, accounts logged in).
    - docs/demo/slides.md: 7–9 Marp-compatible slides (title, problem, roles, architecture, flow
      diagram, ER diagram, key rules, screenshots, testing results, what I learned).
 8. docs/SUBMISSION.md: checklist mapping each PDF deliverable to its file path and status, plus
    the repo URL and the tag to review (v1.0.0).
 9. Final pass: `dotnet build -warnaserror` (fix or justify warnings), `npm run build` with no
    TypeScript errors, all tests green, no TODO/console.log/debug output left, no secrets
    committed (`git grep -i password` shows only docs/demo text), .gitignore covers bin/ obj/
    node_modules/ wwwroot build and Playwright output. LEARNING_NOTES.md Day 6 (integration
    tests with WebApplicationFactory, component tests with Vitest, end-to-end tests with
    Playwright, what to learn next).

5. ACCEPTANCE CRITERIA
- [ ] A fresh clone set up from README alone runs in both modes and passes all tests
- [ ] docs/testing/test-cases.xlsx has 45+ executed cases with actual results; all Pass, or
      failures documented with a reason and owner
- [ ] docs/user-guide/USER_GUIDE.md has a screenshot of every module for every role
- [ ] docs/diagrams/ contains process-flow and ER as Mermaid source + PNG/PDF
- [ ] docs/db/ contains the schema script and the dump with sample data
- [ ] README has setup, stack rationale, default admin and one login per role
- [ ] DEMO_SCRIPT.md is rehearsable in 10 minutes; slides.md exists
- [ ] docs/SUBMISSION.md maps every PDF deliverable to a path
- [ ] Git history shows daily tags day-1..day-6 and v1.0.0

6. VERIFICATION
    dotnet build -warnaserror && dotnet test                            -> green
    npm test --prefix client && npm run build --prefix client           -> green
    dotnet run --project tools/Ims.DocTools -- test-sheet               -> test-cases.xlsx written
    (terminal 1) dotnet run --project src/Ims.Api -- seed --demo-data
                 dotnet run --project src/Ims.Api --launch-profile http
    (terminal 2) npx --prefix client playwright test                    -> smoke + screenshots
                 node client/e2e/render-diagrams.ts (or the documented npm script)
    git log --oneline --decorate                                        -> readable history, all tags
Rehearse the demo script once with a timer.

7. DEMO NOTE
This is demo day: run DEMO_SCRIPT.md end-to-end for the mentor (10 minutes), finishing on the
test-case sheet and the submission checklist.

8. COMMIT & PUSH
    dotnet build && dotnet test && npm run build --prefix client && npm test --prefix client
    git add -A
    git commit -m "docs: test-case sheet, user guide with screenshots, diagrams, demo script and final README"
    git push origin main
    git tag -a day-6 -m "Day 6: QA, docs, demo"
    git tag -a v1.0.0 -m "Submission: Inventory Management System v1.0.0"
    git push origin day-6 v1.0.0
(Defect fixes earlier in the day are separate commits such as
 "fix(inventory): block delivery to an archived delivery point".)

9. GUARDRAILS
- Do not break prior features: no new features; every fix comes with a regression test.
- If anything is ambiguous, choose sensibly and record the decision in DECISIONS.md.
- Report test results honestly; a failing case is documented, never marked Pass.
- Screenshots use seeded demo accounts only; no real personal data in docs.
