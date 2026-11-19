-- Tests for ims_schema_postgres.sql. Run on a scratch database right after loading the schema.
-- Each check prints "ok" or "FAIL" as a NOTICE.
\set ON_ERROR_STOP 1
SET client_min_messages = notice;

CREATE FUNCTION pg_temp.expect_fail(label text, stmt text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE stmt;
    EXCEPTION WHEN others THEN
        RAISE NOTICE 'ok    %  ->  %', label, SQLERRM;
        RETURN;
    END;
    RAISE NOTICE 'FAIL  %  (no error was raised)', label;
END $$;

CREATE FUNCTION pg_temp.expect(label text, actual text, wanted text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    IF actual IS NOT DISTINCT FROM wanted THEN
        RAISE NOTICE 'ok    %  ->  %', label, actual;
    ELSE
        RAISE NOTICE 'FAIL  %  ->  got %, wanted %', label, actual, wanted;
    END IF;
END $$;

SELECT pg_temp.expect('sample unit status', (SELECT status || ' at ' || current_point FROM unit_current_status WHERE barcode = '2440701AK2057'), 'DELIVERED at Refrigerator Delivery Point 1');
SELECT pg_temp.expect('sample history rows', (SELECT count(*)::text FROM inventory_transactions WHERE unit_id = 1), '3');
SELECT pg_temp.expect('compressor attached to sample unit', (SELECT u.barcode FROM unit_attached_components a JOIN units u ON u.id = a.unit_id WHERE a.component_barcode = '94A600321852'), '2440701AK2057');

SELECT pg_temp.expect_fail('update history',  $q$UPDATE inventory_transactions SET reason = 'edited' WHERE id = 1$q$);
SELECT pg_temp.expect_fail('delete history',  $q$DELETE FROM inventory_transactions WHERE id = 1$q$);

INSERT INTO units (barcode, product_id, registered_at, registered_by) VALUES ('TESTUNIT0001', 1, now(), 2);
SELECT pg_temp.expect_fail('component already attached to another unit',
  $q$INSERT INTO inventory_transactions (unit_id, event_type, point_id, component_id, component_barcode, performed_by, occurred_at)
     SELECT id, 'COMPONENT_TRACKED', 1, 1, '94A600321852', 2, now() FROM units WHERE barcode = 'TESTUNIT0001'$q$);
SELECT pg_temp.expect_fail('wrong point type (receive at a delivery point)',
  $q$INSERT INTO inventory_transactions (unit_id, event_type, point_id, performed_by, occurred_at)
     SELECT id, 'RECEIVED', 4, 3, now() FROM units WHERE barcode = 'TESTUNIT0001'$q$);
SELECT pg_temp.expect_fail('reversal with a too-short reason',
  $q$INSERT INTO inventory_transactions (unit_id, event_type, point_id, reverses_id, reason, performed_by, occurred_at) VALUES (1, 'REVERSED', 2, 3, 'no', 1, now())$q$);
SELECT pg_temp.expect_fail('lowercase barcode',
  $q$INSERT INTO units (barcode, product_id, registered_at, registered_by) VALUES ('abc12345', 1, now(), 2)$q$);
SELECT pg_temp.expect_fail('duplicate product name, different case',
  $q$INSERT INTO products (name) VALUES ('refrigerator')$q$);
SELECT pg_temp.expect_fail('user with an unknown role',
  $q$INSERT INTO users (username, password, employee_no, role) VALUES ('x', '!', '99999', 'SUPERUSER')$q$);

-- Reverse the delivery: the unit goes back in stock at its receive point
INSERT INTO inventory_transactions (unit_id, event_type, point_id, reverses_id, reason, performed_by, occurred_at)
VALUES (1, 'REVERSED', 2, 3, 'Wrong unit scanned at delivery', 1, now());
SELECT pg_temp.expect('status after delivery reversal', (SELECT status || ' at ' || current_point FROM unit_current_status WHERE barcode = '2440701AK2057'), 'IN_STOCK at Refrigerator Receive Point 1');
SELECT pg_temp.expect_fail('reverse the same row twice',
  $q$INSERT INTO inventory_transactions (unit_id, event_type, point_id, reverses_id, reason, performed_by, occurred_at) VALUES (1, 'REVERSED', 2, 3, 'second attempt', 1, now())$q$);
SELECT pg_temp.expect_fail('reverse a reversal',
  $q$INSERT INTO inventory_transactions (unit_id, event_type, point_id, reverses_id, reason, performed_by, occurred_at)
     SELECT 1, 'REVERSED', 2, id, 'undo the undo', 1, now() FROM inventory_transactions WHERE reverses_id = 3$q$);

-- Reverse the component entry: the compressor can be tracked on another unit
INSERT INTO inventory_transactions (unit_id, event_type, point_id, reverses_id, reason, performed_by, occurred_at)
VALUES (1, 'REVERSED', 1, 1, 'Component scanned on the wrong unit', 1, now());
INSERT INTO inventory_transactions (unit_id, event_type, point_id, component_id, component_barcode, performed_by, occurred_at)
SELECT id, 'COMPONENT_TRACKED', 1, 1, '94A600321852', 2, now() FROM units WHERE barcode = 'TESTUNIT0001';
SELECT pg_temp.expect('compressor after component reversal', (SELECT u.barcode FROM unit_attached_components a JOIN units u ON u.id = a.unit_id WHERE a.component_barcode = '94A600321852'), 'TESTUNIT0001');

-- Audit log is append-only too
INSERT INTO audit_log (action, entity_type, entity_id, entity_repr, old_values, new_values, ip_address)
VALUES ('UPDATE', 'product', '1', 'Refrigerator', '{"name": "Fridge"}', '{"name": "Refrigerator"}', '127.0.0.1');
SELECT pg_temp.expect_fail('update audit log', $q$UPDATE audit_log SET action = 'CREATE'$q$);
SELECT pg_temp.expect_fail('delete audit log', $q$DELETE FROM audit_log$q$);

-- New rows get ids after the sample data (identity counters were moved past the seeded ids).
-- Exact numbers are not checked: a failed insert uses up an id, which is normal in PostgreSQL.
INSERT INTO products (name) VALUES ('Water Dispenser');
SELECT pg_temp.expect('new product id comes after the sample ids', (SELECT (id > 3)::text FROM products WHERE name = 'Water Dispenser'), 'true');

SELECT version();
