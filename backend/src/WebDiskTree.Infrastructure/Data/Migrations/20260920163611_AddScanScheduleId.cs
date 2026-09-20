using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebDiskTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScanScheduleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "Scans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scans_ScheduleId",
                table: "Scans",
                column: "ScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scans_ScheduleId",
                table: "Scans");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "Scans");
        }
    }
}
