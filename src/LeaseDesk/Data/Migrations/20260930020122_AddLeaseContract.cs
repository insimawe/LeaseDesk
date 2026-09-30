using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaseDesk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LessorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LessorRepresentative = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LessorAddress = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    LesseeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LesseeAddress = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    PropertyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PropertyAddress = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    UnitType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    MaxOccupants = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TermYears = table.Column<int>(type: "INTEGER", nullable: false),
                    SigningDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SigningPlace = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "TEXT", nullable: false),
                    PostDatedCheckCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AdvanceMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    SecurityDepositMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiptAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ReceiptPayer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WitnessOneName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WitnessTwoName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contracts");
        }
    }
}
