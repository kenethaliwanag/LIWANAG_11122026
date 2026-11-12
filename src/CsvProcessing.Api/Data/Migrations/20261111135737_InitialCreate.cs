using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CsvProcessing.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessingRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NumericColumnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UploadedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ColumnAggregates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessingRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ColumnIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ValueCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Sum = table.Column<decimal>(type: "TEXT", nullable: true),
                    Average = table.Column<decimal>(type: "TEXT", nullable: true),
                    Minimum = table.Column<decimal>(type: "TEXT", nullable: true),
                    Maximum = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnAggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColumnAggregates_ProcessingRuns_ProcessingRunId",
                        column: x => x.ProcessingRunId,
                        principalTable: "ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColumnAggregates_ProcessingRunId",
                table: "ColumnAggregates",
                column: "ProcessingRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColumnAggregates");

            migrationBuilder.DropTable(
                name: "ProcessingRuns");
        }
    }
}
