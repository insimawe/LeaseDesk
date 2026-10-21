using LeaseDesk.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaseDesk.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LeaseDeskDbContext))]
    [Migration("20261021120000_AddContractPageHtml")]
    public class AddContractPageHtml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PageHtml",
                table: "Contracts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PageHtml",
                table: "Contracts");
        }
    }
}
