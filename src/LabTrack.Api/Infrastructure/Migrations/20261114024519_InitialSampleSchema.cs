using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabTrack.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSampleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PatientReference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SpecimenType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CollectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabSampleId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_Samples_LabSampleId",
                        column: x => x.LabSampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LabSampleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalyteCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AnalyteName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceLow = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    ReferenceHigh = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    Flag = table.Column<string>(type: "text", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Results_Samples_LabSampleId",
                        column: x => x.LabSampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_LabSampleId_OccurredAt",
                table: "Events",
                columns: new[] { "LabSampleId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Results_LabSampleId_AnalyteCode",
                table: "Results",
                columns: new[] { "LabSampleId", "AnalyteCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Samples_AccessionNumber",
                table: "Samples",
                column: "AccessionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropTable(
                name: "Samples");
        }
    }
}
