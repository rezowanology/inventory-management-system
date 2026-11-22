using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ims.Api.Data.Migrations
{
    /// <summary>
    /// The database "safety net" for inventory rules (the application's InventoryService is the first line of defence).
    /// A BEFORE INSERT trigger on inventory_transactions rejects:
    ///  (1) a row whose point has the wrong type for its event (TRACK / RECEIVE / DELIVERY),
    ///  (2) a component barcode that is already attached (reversed rows do not count),
    ///  (3) reversing a row that is itself a REVERSED row.
    /// </summary>
    public partial class TransactionChecks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PL/pgSQL note: the expected point type is computed into a variable FIRST. A CASE inside an
            // IF condition would end the condition at its first THEN (syntax error).
            migrationBuilder.Sql(@"
CREATE FUNCTION check_transaction() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    expected_type text;
    actual_type   text;
    target_event  text;
BEGIN
    expected_type := CASE NEW.event_type
        WHEN 'COMPONENT_TRACKED' THEN 'TRACK'
        WHEN 'RECEIVED'          THEN 'RECEIVE'
        WHEN 'DELIVERED'         THEN 'DELIVERY'
        ELSE NULL END;

    IF expected_type IS NOT NULL THEN
        SELECT point_type INTO actual_type FROM points WHERE id = NEW.point_id;
        IF actual_type IS DISTINCT FROM expected_type THEN
            RAISE EXCEPTION 'point % has type %, but % needs a % point',
                NEW.point_id, actual_type, NEW.event_type, expected_type;
        END IF;
    END IF;

    IF NEW.event_type = 'COMPONENT_TRACKED' THEN
        IF EXISTS (
            SELECT 1 FROM inventory_transactions t
            WHERE t.event_type = 'COMPONENT_TRACKED'
              AND t.component_barcode = NEW.component_barcode
              AND NOT EXISTS (SELECT 1 FROM inventory_transactions r WHERE r.reverses_id = t.id)
        ) THEN
            RAISE EXCEPTION 'component barcode % is already attached to a unit', NEW.component_barcode;
        END IF;
    END IF;

    IF NEW.event_type = 'REVERSED' THEN
        SELECT event_type INTO target_event FROM inventory_transactions WHERE id = NEW.reverses_id;
        IF target_event = 'REVERSED' THEN
            RAISE EXCEPTION 'a reversal cannot itself be reversed (row %)', NEW.reverses_id;
        END IF;
    END IF;

    RETURN NEW;
END $$;");
            migrationBuilder.Sql(@"CREATE TRIGGER trg_tx_check BEFORE INSERT ON inventory_transactions
    FOR EACH ROW EXECUTE FUNCTION check_transaction();");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_tx_check ON inventory_transactions;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS check_transaction();");
        }
    }
}
