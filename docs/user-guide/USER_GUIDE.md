# Inventory Management System: user guide

For people who use the system for the first time. No technical knowledge needed. All screenshots use the demo accounts and demo data.

**Contents:** [1. What the system does](#1-what-the-system-does) · [2. Sign in, register, change password](#2-sign-in-register-change-password) ·
[3. Component Tracker](#3-component-tracker) · [4. Receive User](#4-receive-user) · [5. Delivery User](#5-delivery-user) ·
[6. Report Viewer](#6-report-viewer) · [7. Admin](#7-admin) · [8. Messages explained](#8-messages-explained) · [9. FAQ and troubleshooting](#9-faq-and-troubleshooting) · [10. Glossary](#10-glossary)

## 1. What the system does

The system follows **one physical unit** (for example one refrigerator, identified by the barcode on it) through three steps:

1. **Component tracking**: the parts built into the unit (compressor, thermostat...) are scanned and attached to it.
2. **Receiving**: the unit arrives at a **receive point** and is now *in stock*.
3. **Delivery**: the unit leaves from a **delivery point** and is *delivered*.

Everything that happens is written to a **history that can never be edited or deleted**. If somebody made a mistake, an **Admin** adds a *reversal*
(with a reason) and the correct entry is made again. Whether a unit is in stock is always worked out from that history.

Every person has **one role**, and the menu shows only what that role may do:

| Role | Demo user | May |
|---|---|---|
| Component Tracker | `tracker` | track components; see the dashboard |
| Receive User | `receiver` | receive units; see the dashboard |
| Delivery User | `deliverer` | deliver units; see the dashboard |
| Report Viewer | `viewer` | read the history and the three reports; export; see the dashboard |
| Admin | `admin` | everything: administration, reversals, reports, audit log |

**Scanning:** a USB barcode scanner types the barcode and presses **Enter**. Typing by hand works the same way: type the barcode, press Enter.
Barcodes are turned into capital letters automatically and may contain only **A-Z, 0-9 and "-"**, 6 to 32 characters.

## 2. Sign in, register, change password

**Sign in.** Open the address you were given (for example `http://localhost:5080`), type your user name and password, press **Sign in**.
Operational roles land directly on their scan screen; the others land on the dashboard.

![Sign-in page](img/01-login.png)

**Register.** No account yet? Choose *Register*, fill in the form and send it. Your account is created as a **Report Viewer** but it is **not active**:
an Admin must activate it before you can sign in ("This account is not active").

![Registration page](img/02-register.png)

**Change your password.** Open your name (top right) and choose *Change password*. Type the current password, then the new one twice.
It must be at least 8 characters with upper and lower case letters, a digit and a symbol. All roles can do this.

![Change password](img/09-change-password.png)

**Forgot your password?** Ask an Admin to reset it; there is no e-mail reset.

## 3. Component Tracker

Sign in as `tracker`. The dashboard shows only your quick action, **Track components**.

![Dashboard of the tracker](img/10-dashboard-tracker.png)

1. Open **Operations > Track components**. The cursor is already in the barcode box. **Scan the unit's barcode** (the product barcode) and press Enter.
2. A barcode that is **new** shows an amber card ("Not registered yet. The first scan registers it") and asks for the **product**. Choose it.

   ![Track: a new unit](img/11-track-new-unit.png)

3. Choose the **track point** (it is remembered for the rest of the browser session).
4. For every part: choose the **component** type, scan the **component's barcode**, press Enter. The row appears in the list.
   Scanning the same component twice in this form shows a **yellow warning** and the row is **not added again**:

   ![Track: duplicate scan warning](img/12-track-duplicate-warning.png)

   ![Track: a batch of components](img/13-track-batch.png)

5. Press **Save components**. All rows are saved together, or none are. The form clears and the cursor returns to the barcode box for the next unit.

   ![Track: saved](img/14-track-saved.png)

A component barcode can belong to **only one unit** at a time. If it is already on another unit you get a red error that names that unit.
You cannot track components on a unit that has already been delivered.

If you open a screen you are not allowed to use, you see **Forbidden**:

![Forbidden page](img/15-forbidden.png)

## 4. Receive User

Sign in as `receiver`. Quick action: **Receive units**.

![Dashboard of the receiver](img/16-dashboard-receiver.png)

1. **Operations > Receive**. Scan the unit's barcode and press Enter. (A unit that was never seen before also asks for its product.)
2. Choose the **receive point**, press **Receive**. The unit is now *in stock* at that point.

   ![Receive: saved](img/17-receive-saved.png)

3. Scanning a unit that is **already in stock** gives a **yellow Duplicate scan warning** that tells you where, when and by whom it was received. Nothing is saved.

   ![Receive: duplicate scan](img/18-receive-duplicate-warning.png)

A unit that has been **delivered** cannot be received again; an Admin must first reverse its delivery.

## 5. Delivery User

Sign in as `deliverer`. Quick action: **Deliver units**.

![Dashboard of the deliverer](img/19-dashboard-deliverer.png)

1. **Operations > Deliver**. Scan the unit's barcode. The card must say **In stock**; otherwise the **Deliver** button stays off and a message explains why
   (unknown barcode, not received yet, or already delivered).

   ![Deliver: unit in stock](img/20-deliver-in-stock.png)

2. Choose the **delivery point**, press **Deliver**. The unit leaves the stock.

   ![Deliver: saved](img/21-deliver-saved.png)

## 6. Report Viewer

Sign in as `viewer`. You can read everything but change nothing: there are no Reverse buttons and no forms that write data.

![Dashboard of the viewer](img/22-dashboard-viewer.png)

**History** (*Records > History*). Every step ever recorded, newest first. Filter by event, product, point, user and date range; search by unit barcode,
component barcode or product; page through the results. **Export CSV** and **Export Excel** download exactly what the filters show.

![History as a viewer](img/23-history-viewer.png)

**Stock report** (*Reports > Stock*). One row per unit that is in stock right now: receive point, who received it and when, **days in stock**, number of components.
The chips above the table show how many units are at each receive point (click one to filter).

![Stock report](img/24-report-stock.png)

**Summary report** (*Reports > Summary*). Choose a date range (the last 30 days if you choose nothing; at most 366 days). The cards give the totals and the
chart shows tracked, received and delivered rows per day. The example below is the sample week of the assignment: 1 tracked, 1 received, 1 delivered.

![Summary report with chart](img/25-report-summary.png)

**Barcode history** (*Reports > Barcode history*). Type a **unit barcode or a component barcode**. You see the unit's card and its complete timeline. If a component
was attached to several units over time, you choose the unit.

![Barcode history of the sample unit](img/26-report-barcode-sample.png)

The **audit log** is for Admins only; a viewer who opens it sees *Forbidden*.

![Forbidden for the viewer](img/27-forbidden-viewer.png)

## 7. Admin

Sign in as `admin`. You have every menu: Operations, Records, Reports and **Admin**.

![Dashboard of the admin](img/03-dashboard-admin.png)

**Dashboard.** Cards: units in stock now, received / delivered / reversed today, registered today, total units. Below: stock by receive point,
**Stuck in stock the longest** (click a barcode to see its history) and recent activity.

### Master data: products, components, points

*Admin > Products / Components / Points*: search, status filter (Active / Archived / All), paging, **New**, **Edit**, **Delete**, **Restore**, and the export buttons.
Names must be unique (capital letters do not matter). Points have a type: TRACK, RECEIVE or DELIVERY.

![Products](img/04-manage-products.png)
![Components](img/05-manage-components.png)
![Points](img/06-manage-points.png)
![New product form](img/07-product-form.png)

**Delete** asks for confirmation. A row that is **already used** is not erased: it is *archived* (hidden from the dropdowns, still shown in the history, can be restored).
A row nobody uses is removed for good.

### Users

*Admin > Users*: create users with one role, edit them, **activate / deactivate**, reset a password. Use the **Pending registration** filter to find people who registered
themselves and wait for activation. Users are never deleted. You cannot deactivate yourself, and the last active Admin cannot be deactivated or demoted.
A deactivated person is **signed out on their next click**.

![Users](img/08-manage-users.png)

### History and reversals

*Records > History* shows a **Reverse** button on every row that may be reversed. A greyed button has a tooltip saying why not.

![History as an admin](img/28-history-admin.png)

Press **Reverse**. The screen shows the original row and **what will change** (for example "Unit ... will return to stock at Refrigerator Receive Point 1").
Type a **reason of at least 5 characters** and confirm.

![Reverse: confirm](img/29-reverse-confirm.png)

| Reversing a... | Allowed when | Result |
|---|---|---|
| component row | always (once) | the component is detached and can be used again |
| receive row | the unit has not been delivered | the unit is no longer in stock |
| delivery row | it is the latest delivery | the unit is in stock again at its receive point |

A row can be reversed **once**, and a reversal can never be reversed. The old row is **never changed**: it stays, struck through, and a new REVERSED row is added.
After the reversal, a link takes you to the right scan screen with the barcode filled in, so you can enter the correct data.

![Reverse: done](img/30-reverse-done.png)

The barcode history shows the whole story, with the reason:

![Barcode history with a reversal](img/31-report-barcode-reversal.png)

The reversed unit appears in the stock report again:

![Stock report as an admin](img/32-report-stock-admin.png)

### Audit log

*Admin > Audit log* (Admins only) lists who changed what: creating, changing, archiving and restoring products, components, points and users, role changes,
activation, password changes, and every **login, failed login and logout**. Filter by user, action, entity type and date; search; export.
Passwords are **never** recorded.

![Audit log](img/33-audit-log.png)

Press **Show values** on a row to see the **old and new value** side by side. Only the properties that changed are stored.

![Audit log: old and new values](img/34-audit-values.png)

The dashboard follows every change at once:

![Dashboard after the changes](img/35-dashboard-admin-after.png)

## 8. Messages explained

| What you see | Meaning | What to do |
|---|---|---|
| **Yellow "Duplicate scan"** (with where, when, by whom) | The unit or component is already in that state. Nothing was saved. | Check the barcode; usually nothing more is needed. |
| **Red error** "Component ... is already attached to unit ..." | That component belongs to another unit. | Check the barcode; ask an Admin to reverse the old entry if it was a mistake. |
| Red error "Unit ... has already been delivered" | A delivered unit cannot be tracked or received. | Ask an Admin to reverse the delivery if it was a mistake. |
| "Barcode must be 6-32 characters: A-Z, 0-9 and "-"" | The barcode has spaces, symbols or the wrong length. | Scan again or type it carefully. |
| "Give a reason of at least 5 characters" | Reversals need a reason. | Write a short explanation. |
| "The start date must be on or before the end date" / "at most 366 days" | Invalid date range in the summary report. | Change the dates. |
| **Forbidden** | Your role is not allowed to open this page. | Use the menu: it shows only what you may open. Ask an Admin if you need another role. |

## 9. FAQ and troubleshooting

**I forgot my password.** Ask an Admin: *Admin > Users > Reset password*. After signing in, change it yourself.

**"This account is not active".** You registered yourself (or an Admin deactivated you). An Admin must activate the account in *Admin > Users*.

**"Your session has ended. Please sign in again."** An Admin deactivated your account, or you changed or reset your password. Sign in again (if the account is inactive, ask an Admin).

**Where is the menu item I need?** The menu is built from your role. If it is not there, your role may not do it.

**I scanned the wrong unit or component.** Ask an Admin to reverse the wrong entry (the history keeps both), then enter the right one.

**The barcode box does not react to the scanner.** Click into the box once; the scanner must send Enter after the code. You can also type the barcode and press Enter.

**A unit I know is not in the stock report.** It is probably delivered, or it was never received. Look it up in *Reports > Barcode history*.

**The export file shows strange characters in Excel.** Use **Export Excel**, or open the CSV via *Data > From text/CSV* and choose UTF-8.

## 10. Glossary

| Term | Meaning |
|---|---|
| **Unit** | One physical product, identified by its product barcode (like a serial number). |
| **Product** | The kind of unit (Refrigerator, Freezer, Air Conditioner...). |
| **Component** | A part built into a unit (Compressor, Thermostat...). Each component has its own barcode and belongs to one unit at a time. |
| **Track point** | A place where components are scanned onto units. |
| **Receive point** | A place where units arrive and are stored. A unit "in stock" is at one receive point. |
| **Delivery point** | A place from where units are sent out. |
| **In stock** | Received and not yet delivered. |
| **History** | The list of every step. It can never be edited or deleted. |
| **Reversal** | A new history row, made by an Admin with a reason, that cancels an earlier row. The earlier row stays visible, struck through. |
| **Archive (soft delete)** | A used product, component or point is hidden from dropdowns but kept for the history; it can be restored. |
| **Audit log** | The Admin's record of who changed what in the administration, and of logins. It is separate from the history. |
| **Duplicate scan** | Scanning something that is already in that state. It is a warning, not an error, and nothing is saved. |
