# ER diagram (DRAFT, Day 1)

Matches the EF Core model. Final image version comes on Day 5/6. `inventory_transactions` and `audit_log` are
append-only (EF interceptor + PostgreSQL trigger). There is no status column anywhere: status is derived from history.

```mermaid
erDiagram
    users ||--o{ units : "registered_by"
    users ||--o{ inventory_transactions : "performed_by"
    users ||--o{ audit_log : "actor (null = system)"
    products ||--o{ units : "product_id"
    units ||--o{ inventory_transactions : "unit_id"
    points ||--o{ inventory_transactions : "point_id"
    components ||--o{ inventory_transactions : "component_id (tracked rows only)"
    inventory_transactions |o--o| inventory_transactions : "reverses_id (unique)"

    users {
        bigint id PK
        text user_name UK
        text password_hash
        text first_name
        text last_name
        text employee_no UK
        text role "ADMIN | COMPONENT_TRACKER | RECEIVE_USER | DELIVERY_USER | REPORT_VIEWER"
        bool is_active
    }
    products { bigint id PK
        text name "unique among non-deleted"
        bool is_deleted
        timestamptz deleted_at }
    components { bigint id PK
        text name "unique among non-deleted"
        bool is_deleted }
    points { bigint id PK
        text name
        text point_type "TRACK | RECEIVE | DELIVERY"
        bool is_deleted }
    units { bigint id PK
        text barcode UK
        bigint product_id FK
        timestamptz registered_at
        bigint registered_by FK }
    inventory_transactions { bigint id PK
        bigint unit_id FK
        text event_type "COMPONENT_TRACKED | RECEIVED | DELIVERED | REVERSED"
        bigint point_id FK
        bigint component_id FK "nullable"
        text component_barcode "nullable"
        bigint reverses_id FK "nullable, unique"
        text reason
        bigint performed_by FK
        timestamptz occurred_at }
    audit_log { bigint id PK
        bigint actor_id FK "nullable"
        text action
        text entity_type
        text entity_id
        jsonb old_values
        jsonb new_values
        inet ip_address
        timestamptz created_at }
```
