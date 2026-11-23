# Submission checklist

**Project:** Inventory Management System (industrial attachment assignment, brief dated 3 Nov 2026)
**Repository:** https://github.com/rezowanology/inventory-management-system
**Tag to review:** `v1.0.0` (daily tags `day-1` ... `day-6` are in the history)

## Deliverables

| # | Deliverable (from the brief) | File / place | Status |
|---|---|---|---|
| 1 | Process flow diagram (image/PDF) | [docs/diagrams/process-flow.png](diagrams/process-flow.png), [.pdf](diagrams/process-flow.pdf); Mermaid source [process-flow.md](diagrams/process-flow.md) | Done |
| 2 | ER diagram | [docs/diagrams/er-diagram.png](diagrams/er-diagram.png), [.pdf](diagrams/er-diagram.pdf); Mermaid source [er-diagram.md](diagrams/er-diagram.md) (matches the database one-to-one) | Done |
| 3 | Database script with sample data | [docs/db/schema.sql](db/schema.sql) (all migrations, idempotent) and [docs/db/inventory_with_sample_data.sql](db/inventory_with_sample_data.sql) (pg_dump incl. the 3 sample rows; load check: 3 rows). How to load: [docs/db/README.md](db/README.md) | Done |
| 4 | Source code in Git with a real commit history | this repository: `src/Ims.Api`, `client`, `tests`, `tools`; `git log --oneline --decorate` | Done |
| 5 | README: setup, default admin login, **one login per role**, stack and reasons | [README.md](../README.md) | Done |
| 6 | Test-case sheet (case, expected, actual, pass/fail) | [docs/testing/test-cases.xlsx](testing/test-cases.xlsx) and [test-cases.csv](testing/test-cases.csv): 97 cases, 96 Pass, 1 Not run (open the export in desktop Excel; owner: Rezowan) | Done (see note) |
| 7 | User guide with screenshots of every module | [docs/user-guide/USER_GUIDE.md](user-guide/USER_GUIDE.md) + 35 screenshots in [docs/user-guide/img/](user-guide/img/) | Done |
| 8 | 10-minute demo (live or video) | [docs/demo/DEMO_SCRIPT.md](demo/DEMO_SCRIPT.md) (timed script, barcodes, checklist) | Script ready; the live demo is yours to give (not yet rehearsed with a timer) |
| 9 | Slides | [docs/demo/slides.md](demo/slides.md) (Marp format; 10 slides) | Done |

## Also in the repository

| What | Where |
|---|---|
| Decisions (every judgment call, assumptions A1-A15, sweep checklist) | [DECISIONS.md](../DECISIONS.md) |
| Daily progress notes for the team lead | [docs/PROGRESS.md](PROGRESS.md) |
| Learning notes (.NET and React concepts, per day) | [docs/LEARNING_NOTES.md](LEARNING_NOTES.md) |
| Automated tests | `tests/Ims.Tests` (114), `client/src/**/*.test.tsx` (39), `client/e2e` (7 Playwright specs) |
| Regenerate the SQL files | [scripts/export-sql.ps1](../scripts/export-sql.ps1) |

## Test results (v1.0.0)

| Suite | Result |
|---|---|
| `dotnet build -warnaserror` | 0 warnings, 0 errors |
| `dotnet test` | 114 passed |
| `npm test --prefix client` | 39 passed |
| `npm run build --prefix client` | no TypeScript errors |
| Playwright (`npm run e2e --prefix client`, demo mode) | 7 passed (smoke flow, 5-role UI matrix, screenshots) |
| Test-case sheet | 97 cases: 96 Pass, 0 Fail, 1 Not run |

## Notes for the reviewer

- Demo logins are in the README (admin and one account per role). They are demo-only passwords.
- The `ims_user` database password is never in the repository: it is stored in .NET user-secrets (README, Setup).
- Open questions for the mentor (assumptions A3, A6, A7, A12) are listed in `CLAUDE.md` section 9 and `DECISIONS.md`; the current behaviour is documented there.
- One manual check is open: opening the exported `.xlsx`/`.csv` in desktop Excel (the automated tests open them with ClosedXML).
