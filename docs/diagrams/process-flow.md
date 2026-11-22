# Process flow (DRAFT, Day 1)

One physical unit, identified by its barcode, moves through three operations. Admin can reverse a step. Final image
version comes on Day 5/6.

```mermaid
flowchart TD
    A([Unit barcode scanned]) --> B{Known unit?}
    B -- No --> C[Track or Receive registers the unit<br/>user picks the product]
    B -- Yes --> D{Current status}
    C --> E[Registered]
    D --> E
    E -->|Component Tracker: scan component barcodes| F[COMPONENT_TRACKED rows<br/>component belongs to this unit only]
    F --> E
    E -->|Receive User: choose receive point| G[RECEIVED<br/>status: In stock]
    G -->|Component Tracker: more components| G
    G -->|Delivery User: choose delivery point| H[DELIVERED<br/>status: Delivered, leaves stock]
    H -->|Admin reversal + reason| G
    G -->|Admin reversal + reason, only if not delivered| E
    F -->|Admin reversal + reason| E
    I[Report Viewer / Admin] -.reads.-> J[(Append-only history)]
    G --> J
    H --> J
    F --> J
```

Rules: delivered units cannot be received again unless the delivery is reversed; a row can be reversed once; a
reversal row can never be reversed; every reversal is a new `REVERSED` row (nothing is edited or deleted).
