using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebDiskTree.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoritePaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FavoritePaths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoritePaths", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoritePaths_Path",
                table: "FavoritePaths",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FavoritePaths");
        }
    }
}
