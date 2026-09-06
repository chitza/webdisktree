using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebDiskTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImdbLookupCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImdbLookupCache",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CacheKey = table.Column<string>(type: "TEXT", nullable: false),
                    ParsedTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ImdbId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImdbLookupCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImdbLookupCache_CacheKey",
                table: "ImdbLookupCache",
                column: "CacheKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImdbLookupCache");
        }
    }
}
