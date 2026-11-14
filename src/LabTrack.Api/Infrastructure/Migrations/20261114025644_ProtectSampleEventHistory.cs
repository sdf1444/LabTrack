using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabTrack.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProtectSampleEventHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION prevent_sample_event_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'Sample event history is append-only'
                        USING ERRCODE = '55000';
                END;
                $$;

                CREATE TRIGGER trg_events_append_only
                BEFORE UPDATE OR DELETE ON "Events"
                FOR EACH ROW EXECUTE FUNCTION prevent_sample_event_mutation();
                ALTER TABLE "Events" ENABLE ALWAYS TRIGGER trg_events_append_only;

                CREATE TRIGGER trg_events_no_truncate
                BEFORE TRUNCATE ON "Events"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_sample_event_mutation();
                ALTER TABLE "Events" ENABLE ALWAYS TRIGGER trg_events_no_truncate;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_events_append_only ON "Events";
                DROP TRIGGER IF EXISTS trg_events_no_truncate ON "Events";
                DROP FUNCTION IF EXISTS prevent_sample_event_mutation();
                """);
        }
    }
}
