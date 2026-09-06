using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebDiskTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScanPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Scans",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Scans");
        }
    }
}
