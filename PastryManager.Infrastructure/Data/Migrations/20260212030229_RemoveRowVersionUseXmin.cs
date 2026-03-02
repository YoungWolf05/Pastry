using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PastryManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRowVersionUseXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the custom RowVersion columns
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Accounts");
            
            // Note: xmin is a PostgreSQL system column, it already exists - no need to add it
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: xmin is a system column, we don't drop it
            
            // Re-add the custom RowVersion columns
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Transactions",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Accounts",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
