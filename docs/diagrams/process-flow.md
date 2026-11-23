# Process flow (final, Day 5)

One physical unit, identified by its product barcode, goes through **component tracking -> receiving -> delivery**.
Each role works in its own lane; the Admin can reverse a step and the Report Viewer only reads.
Every screen talks to the API in the same way (shown once in the second diagram): **React -> ASP.NET Core API -> PostgreSQL**.

## 1. Business flow by role

```mermaid
flowchart TD
    subgraph TR["Component Tracker (ops.track)"]
        T1([Scan unit barcode]) --> T2{Unit known?}
        T2 -- "No: first scan" --> T3[Choose the product<br/>unit is registered]
        T2 -- Yes --> T4{Unit delivered?}
        T3 --> T5[Scan component barcodes<br/>and choose the track point]
        T4 -- Yes --> TE1[/Error: no tracking after delivery/]
        T4 -- No --> T5
        T5 --> T6{Component barcode<br/>already used?}
        T6 -- "On this unit" --> TW[/Warning: duplicate scan<br/>when, where, by whom - nothing saved/]
        T6 -- "On another unit" --> TE2[/Error: names the other unit/]
        T6 -- No --> T7[(COMPONENT_TRACKED rows saved<br/>all or nothing)]
    end

    subgraph RC["Receive User (ops.receive)"]
        R1([Scan unit barcode]) --> R2{Status}
        R2 -- "In stock" --> RW[/Warning: duplicate scan<br/>point, time, user - nothing saved/]
        R2 -- Delivered --> RE[/Error: reverse the delivery first/]
        R2 -- "Unknown or Registered" --> R3[Choose the receive point<br/>new unit: choose the product]
        R3 --> R4[(RECEIVED row saved<br/>unit is In stock)]
    end

    subgraph DL["Delivery User (ops.deliver)"]
        D1([Scan unit barcode]) --> D2{In stock?}
        D2 -- "Unknown / not received / already delivered" --> DE[/Error: clear message/]
        D2 -- Yes --> D3[Choose the delivery point]
        D3 --> D4[(DELIVERED row saved<br/>unit leaves the stock count)]
    end

    subgraph AD["Admin (history.reverse, masterdata.manage, users.manage, audit.view)"]
        A1[Open a history row] --> A2{Allowed to reverse?}
        A2 -- "Already reversed, or a REVERSED row" --> AE[/Blocked with an explanation/]
        A2 -- "Receive of a delivered unit" --> AE
        A2 -- "Delivery that is not the latest" --> AE
        A2 -- Yes --> A3[Give a reason of 5+ characters]
        A3 --> A4[(New REVERSED row saved<br/>old row stays, shown struck through)]
        A5[Manage products, components, points, users<br/>activate / deactivate, change roles] --> A6[(audit_log row with old and new values)]
    end

    subgraph RV["Report Viewer and Admin (reports.view, history.view)"]
        V1[Stock report] --- V2[Summary report with chart]
        V2 --- V3[Barcode history incl. reversals]
        V3 --- V4[CSV / Excel export]
    end

    T7 --> H[(Append-only inventory history<br/>status and stock are DERIVED from it)]
    R4 --> H
    D4 --> H
    A4 --> H
    H --> RV
    H -. "reversal: component detached" .-> T5
    H -. "reversal of a delivery: back in stock<br/>at the original receive point" .-> R4
    A4 -. "reversal of a receive: unit is Registered again" .-> R3

    classDef warn fill:#fff3cd,stroke:#997404
    classDef err fill:#f8d7da,stroke:#842029
    classDef store fill:#d1e7dd,stroke:#0f5132
    class TW,RW warn
    class TE1,TE2,RE,DE,AE err
    class T7,R4,D4,A4,A6,H store
```

Everyone who is signed in can also open the **dashboard** (read-only) and **change their own password**.
A deactivated user is signed out on their very next request.

## 2. What happens on every click: React -> API -> database

```mermaid
sequenceDiagram
    actor U as User (browser)
    participant R as React app (client/)
    participant A as ASP.NET Core API (src/Ims.Api)
    participant S as InventoryService / report services
    participant D as PostgreSQL (ims)

    U->>R: scan barcode, press Enter / click Save
    R->>A: fetch /api/... with the sign-in cookie and the X-XSRF-TOKEN header
    A->>A: check cookie, antiforgery token and the policy of the endpoint (403 if the role may not)
    A->>S: controller calls the service (rules live only here)
    S->>D: BEGIN, lock the unit row (SELECT FOR UPDATE), derive status from history
    alt a rule is broken
        S-->>A: DomainException (422) or DuplicateScanException (409)
        A-->>R: ProblemDetails JSON
        R-->>U: red error or yellow duplicate-scan warning
    else all rules pass
        S->>D: INSERT history row(s), triggers re-check, COMMIT
        A->>D: master-data / user changes also INSERT audit_log rows (same SaveChanges)
        S-->>A: updated unit state
        A-->>R: 200 JSON
        R-->>U: success message, unit card, refreshed lists
    end
```

Rules shown above: a unit is registered by its first scan; tracking is blocked after delivery; receiving only works for a unit
that is neither in stock nor delivered; delivery only for units in stock; a duplicate scan is a *warning*, a broken rule an
*error*; a row can be reversed once; a `REVERSED` row can never be reversed; nothing is ever edited or deleted.
