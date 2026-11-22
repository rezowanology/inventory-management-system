using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ims.Api.Data.Migrations
{
    /// <summary>
    /// Raw-SQL part of the schema that EF cannot model directly:
    /// (1) the append-only trigger on inventory_transactions and audit_log (rule R6, second line of defence),
    /// (2) partial unique indexes: a master-data name is unique (ignoring case) only among NON-deleted rows.
    /// </summary>
    public partial class AppendOnlyAndIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE FUNCTION forbid_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION '% is append-only', TG_TABLE_NAME;
END $$;");
            migrationBuilder.Sql(@"CREATE TRIGGER trg_tx_append_only BEFORE UPDATE OR DELETE ON inventory_transactions
    FOR EACH ROW EXECUTE FUNCTION forbid_change();");
            migrationBuilder.Sql(@"CREATE TRIGGER trg_audit_append_only BEFORE UPDATE OR DELETE ON audit_log
    FOR EACH ROW EXECUTE FUNCTION forbid_change();");

            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_products_name_active ON products (lower(name)) WHERE NOT is_deleted;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_components_name_active ON components (lower(name)) WHERE NOT is_deleted;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_points_name_type_active ON points (lower(name), point_type) WHERE NOT is_deleted;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_points_name_type_active;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_components_name_active;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_products_name_active;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_append_only ON audit_log;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_tx_append_only ON inventory_transactions;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS forbid_change();");
        }
    }
}
