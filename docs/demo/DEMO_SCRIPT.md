# Demo script: 10 minutes, DEMO MODE (one process on http://localhost:5080)

Slides: [slides.md](slides.md). Everything below runs in **one browser tab per person** against **one server**.

## Pre-demo checklist (do this 15 minutes before)

1. **PostgreSQL is running** (Windows service `postgresql-x64-17`): `Get-Service postgresql-x64-17` shows *Running*.
2. **Stop any running API**, then **reset the database** so the numbers are clean (this deletes the data in `ims`):
   ```powershell
   dotnet ef database drop --force --project src/Ims.Api
   dotnet ef database update --project src/Ims.Api
   dotnet run --project src/Ims.Api -- seed --demo-data
   ```
3. **Build the client** and start the server (one process, port 5080):
   ```powershell
   npm run build --prefix client
   dotnet run --project src/Ims.Api --launch-profile http
   ```
4. **Two browser windows** (use a normal window and a private/incognito window, so the two sign-ins do not share cookies):
   - Window A: signed in as **admin** (`admin` / `Admin@12345`).
   - Window B: opens the other accounts one after another (`tracker`, `receiver`, `deliverer`, `viewer`; all `Demo@12345`).
   Open every tab you need beforehand (listed in the script) and sign in. Open the slides in a third place.
5. Have ready: `docs/diagrams/process-flow.png`, `docs/diagrams/er-diagram.png`, `docs/testing/test-cases.xlsx`, `docs/SUBMISSION.md`.
6. Keep this text open: **barcodes to type** (they are new in a freshly reset database):
   - Sample unit (already seeded): `2440701AK2057`, component `94A600321852`
   - New unit for the live flow: `LIVE000001`, components `LIVEC00001A` (Compressor) and `LIVEC00001B` (Thermostat)
   - A unit that is in stock for a long time (demo data): `DEMO000005`

## Timed script

| Time | What you do | What you say / show |
|---|---|---|
| **0:00** | Slides 1-4 (title, problem, roles, architecture) | "A barcode system that follows one physical unit from component tracking to delivery. React in the browser, an ASP.NET Core API, PostgreSQL. Five roles, history that can never be edited, status always derived from it." |
| **1:00** | Window B as `viewer`: open `/ops/receive` -> **Forbidden** page. Then in the browser address bar or a terminal: show that the API says 403 too (`GET /api/ops/...` in DevTools Network, or point to the test "policy matrix"). | "The menu shows only what a role may do, the server enforces it anyway. 5 roles x every endpoint is tested: that is the policy matrix test." |
| **2:00** | Window A (admin): **Admin > Products**: create "Washing Machine"; rename it; delete it (unused: really deleted). Then delete *Refrigerator* (used): the **archive** message appears; restore it. **Admin > Users**: deactivate `viewer`; switch to the viewer tab and click anything: **"Your session has ended"**. Reactivate `viewer`. | "Used master data is archived, not erased, so history stays complete. Deactivating a user ends their session on the very next click." |
| **3:30** | Window A: **Reports > Barcode history**, enter `2440701AK2057`. | "The sample record from the brief: component tracked, received at Receive Point 1, delivered. Three rows, in order, with the exact times." |
| **4:30** | **Live flow with three accounts.** Window B as `tracker`: **Track**, scan `LIVE000001`, product *Refrigerator*, track point, component *Compressor* `LIVEC00001A` Enter, **scan `LIVEC00001A` again** -> yellow *duplicate* warning, add *Thermostat* `LIVEC00001B`, **Save**. Sign in as `receiver`: **Receive** `LIVE000001`, Receive Point 1, Receive; **scan again** -> yellow *Duplicate scan* warning (where, when, by whom). Sign in as `deliverer`: **Deliver** `LIVE000001`, Delivery Point 1. | "Each person only sees their own screen. A repeated scan is a warning, a broken rule is an error, and neither saves anything. All rules are server side; the screens only help." |
| **6:30** | Window A (admin): **History**, search `LIVE000001`, **Reverse** the DELIVERED row. Show the preview ("will return to stock at ..."), try a 2-letter reason (blocked), enter "Delivered by mistake", confirm. Then **Reports > Barcode history** `LIVE000001`. | "Reversals add a row, nothing is edited. The unit is back in stock at its original receive point; the old row is struck through with who, when and why." |
| **7:30** | **Reports > Stock** (the unit is listed again; chips per receive point). **Reports > Summary**: set 2026-03-27 to 2026-03-30 -> 1/1/1 and the chart. Click **Export Excel** and open the file. | "Every number is computed from the history. Exports use the same filters as the screen." |
| **8:30** | **Dashboard** (cards, stock by point, *stuck in stock the longest*, recent activity). **Admin > Audit log**: filter action UPDATE, **Show values** on the product rename from step 2:00. | "Audit rows keep old and new values and never any password. The audit log is separate from the inventory history." |
| **9:15** | Show `docs/diagrams/process-flow.png` and `er-diagram.png`, then `docs/testing/test-cases.xlsx` (97 cases, green). | "The ER diagram matches the database exactly. 114 .NET + 39 Vitest + 7 Playwright tests; every row of the sheet maps to a test." |
| **9:45** | Open `docs/SUBMISSION.md`. **Q&A.** | "Every deliverable has a path here; the tag to review is v1.0.1." |

## If something goes wrong

- **A screen shows "Your session has ended"** after the user-activation step: that is the forced logout working; sign in again.
- **`LIVE000001` already exists** (you rehearsed without resetting): use `LIVE000002` / `LIVEC00002A` / `LIVEC00002B` and say so, or reset the database again.
- **Server not reachable:** check that the API window still runs (`dotnet run ... --launch-profile http`) and use `http://` not `https://`.
- **Excel does not open the CSV nicely:** use *Export Excel*.

## Rehearsal

The script and times were written for a 10-minute run; rehearse it once with a timer and adjust the pauses. (Not rehearsed by the author of this file.)
