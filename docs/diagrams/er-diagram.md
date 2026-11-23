# ER diagram (final, Day 5)

This diagram matches the PostgreSQL schema produced by the EF Core migrations one-to-one (`docs/db/schema.sql`):
same 10 tables, same column names, same keys. Types are written as PostgreSQL types.
Legend: **PK** primary key, **FK** foreign key, **UK** unique (a plain unique index).

```mermaid
erDiagram
    users ||--o{ units : "registered_by_id"
    users ||--o{ inventory_transactions : "performed_by_id"
    users |o--o{ audit_log : "actor_id (null = system)"
    users ||--o{ user_claims : "user_id (cascade)"
    users ||--o{ user_logins : "user_id (cascade)"
    users ||--o{ user_tokens : "user_id (cascade)"
    products ||--o{ units : "product_id"
    units ||--o{ inventory_transactions : "unit_id"
    points ||--o{ inventory_transactions : "point_id"
    components |o--o{ inventory_transactions : "component_id (tracked rows only)"
    inventory_transactions |o--o| inventory_transactions : "reverses_id (unique)"

    users {
        bigint id PK
        varchar user_name "Identity; unique via normalized_user_name"
        varchar normalized_user_name UK
        varchar first_name
        varchar last_name
        varchar employee_no UK
        varchar role "ADMIN, COMPONENT_TRACKER, RECEIVE_USER, DELIVERY_USER, REPORT_VIEWER"
        boolean is_active
        timestamptz last_login_at "null = never signed in"
        text password_hash "Identity, never logged or exported"
        text security_stamp "Identity, renewed to force logout"
        text concurrency_stamp "Identity"
        varchar email "Identity, unused"
        varchar normalized_email "Identity, unused"
        boolean email_confirmed "Identity, unused"
        text phone_number "Identity, unused"
        boolean phone_number_confirmed "Identity, unused"
        boolean two_factor_enabled "Identity, unused"
        timestamptz lockout_end "Identity, unused"
        boolean lockout_enabled "Identity, unused"
        integer access_failed_count "Identity, unused"
    }
    products {
        bigint id PK
        varchar name "unique (lower) among non-deleted rows"
        boolean is_deleted
        timestamptz deleted_at
        timestamptz created_at
        timestamptz updated_at
    }
    components {
        bigint id PK
        varchar name "unique (lower) among non-deleted rows"
        boolean is_deleted
        timestamptz deleted_at
        timestamptz created_at
        timestamptz updated_at
    }
    points {
        bigint id PK
        varchar point_type "TRACK, RECEIVE, DELIVERY"
        varchar name "unique (lower name, point_type) among non-deleted rows"
        boolean is_deleted
        timestamptz deleted_at
        timestamptz created_at
        timestamptz updated_at
    }
    units {
        bigint id PK
        varchar barcode UK "the product barcode = one physical unit"
        bigint product_id FK
        timestamptz registered_at
        bigint registered_by_id FK
    }
    inventory_transactions {
        bigint id PK
        bigint unit_id FK
        varchar event_type "COMPONENT_TRACKED, RECEIVED, DELIVERED, REVERSED"
        bigint point_id FK
        bigint component_id FK "only on COMPONENT_TRACKED"
        varchar component_barcode "only on COMPONENT_TRACKED"
        bigint reverses_id FK "only on REVERSED, unique: a row is reversed once"
        text reason "REVERSED needs 5+ characters"
        bigint performed_by_id FK
        timestamptz occurred_at
        timestamptz created_at
    }
    audit_log {
        bigint id PK
        bigint actor_id FK "null = system"
        varchar action "CREATE, UPDATE, SOFT_DELETE, RESTORE, DELETE, ROLE_CHANGE, ACTIVATE, DEACTIVATE, PASSWORD_CHANGE, PASSWORD_RESET, LOGIN, LOGIN_FAILED, LOGOUT"
        varchar entity_type
        varchar entity_id
        varchar entity_repr
        jsonb old_values "changed properties only"
        jsonb new_values "changed properties only"
        inet ip_address
        timestamptz created_at
    }
    user_claims {
        integer id PK
        bigint user_id FK
        text claim_type
        text claim_value
    }
    user_logins {
        text login_provider PK
        text provider_key PK
        text provider_display_name
        bigint user_id FK
    }
    user_tokens {
        bigint user_id PK
        text login_provider PK
        text name PK
        text value
    }
```

## Notes that the diagram cannot show

- **There is no status column.** The status of a unit (Registered, In stock, Delivered) and the stock count are *derived*
  from `inventory_transactions`: take the rows that are not `REVERSED` and not cancelled by a `REVERSED` row, in time order;
  the last `RECEIVED` / `DELIVERED` row decides. Which components are attached is derived the same way.
- **Append-only.** `inventory_transactions` and `audit_log` can only be inserted into. Two layers enforce it: the EF Core
  `AppendOnlyInterceptor`, and the PostgreSQL function `forbid_change()` with `BEFORE UPDATE OR DELETE` triggers
  (`trg_tx_append_only`, `trg_audit_append_only`). A reversal is a **new** row with `event_type = 'REVERSED'` whose
  `reverses_id` points at the cancelled row (the self-reference `inventory_transactions.reverses_id -> id`).
- **More database rules** (CHECK constraints and a trigger): `ck_tx_component_only_on_tracked` (component columns only on
  COMPONENT_TRACKED rows), `ck_tx_reversal_shape` (REVERSED rows need `reverses_id` and a reason of 5+ characters), and the
  trigger `trg_tx_check` (`check_transaction()`): the point type must match the event (TRACK / RECEIVE / DELIVERY), a component
  barcode cannot be attached twice, and a REVERSED row cannot be reversed.
- **Soft delete.** Master data that is already used is not removed: `is_deleted` is set (hidden from dropdowns, still shown in
  history, restorable). The uniqueness of names is a *partial* unique index on `lower(name) WHERE NOT is_deleted`
  (`ux_products_name_active`, `ux_components_name_active`, `ux_points_name_type_active`). Users are never deleted, only
  deactivated (`is_active`).
- **ASP.NET Core Identity** supplies most of the `users` columns (the ones marked "Identity") and the tables `user_claims`,
  `user_logins`, `user_tokens`. The application adds `first_name`, `last_name`, `employee_no`, `role`, `is_active`,
  `last_login_at`. There are no Identity role tables: the role is the `users.role` text column (one role per user).
  The table is named `users` (EF Core renamed it from `AspNetUsers`); only a few auto-generated constraint names still say `asp_net_users`.
- **All foreign keys are `ON DELETE RESTRICT`** except the three Identity child tables (`CASCADE`).
- **All times are `timestamptz`, stored in UTC**; the app shows them in `App:TimeZone` (Asia/Dhaka).
- `__EFMigrationsHistory` (EF Core's bookkeeping table) is not drawn.
