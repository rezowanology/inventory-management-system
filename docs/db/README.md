# Database scripts

| File | What it is |
|---|---|
| `schema.sql` | Every EF Core migration as ONE idempotent PostgreSQL script: all tables, keys, indexes, CHECK constraints, the append-only triggers (`forbid_change`) and the safety trigger (`check_transaction`). Safe to run on an empty database, or again on an existing one (each step checks `__EFMigrationsHistory`). |
| `inventory_with_sample_data.sql` | A `pg_dump` of a freshly migrated and seeded database: the same structure **plus data**: the 5 demo users (password hashes of the demo passwords from the README), the master data, and the 3-row **sample record** from the brief (unit `2440701AK2057`). No demo data, no personal data. |

Both are generated; do not edit them by hand.

## Load into an empty database

```powershell
createdb -U ims_user ims_review
psql -U ims_user -d ims_review -v ON_ERROR_STOP=1 -f docs/db/inventory_with_sample_data.sql
psql -U ims_user -d ims_review -c "select event_type, occurred_at from inventory_transactions order by id"
```

(`psql` asks for the `ims_user` password.) Use `schema.sql` instead when you want the empty structure only:
`psql -U ims_user -d ims_review -v ON_ERROR_STOP=1 -f docs/db/schema.sql`.

The ER diagram that matches these scripts is `docs/diagrams/er-diagram.md`.

## Regenerate

Stop the running API first, then:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/export-sql.ps1 -Verify
```

What the script does: reads the connection string from the .NET user-secret (it is never printed; the password is passed to
`pg_dump`/`psql` through the `PGPASSWORD` variable of the script's own process only), writes `schema.sql` with
`dotnet ef migrations script --idempotent`, builds a throw-away database `ims_export` (migrate + `seed`), dumps it with
`pg_dump --no-owner --no-privileges`, drops it, and with `-Verify` also loads the dump into a second throw-away database
to prove it works and that `inventory_transactions` has 3 rows. Your real `ims` database is never touched.
