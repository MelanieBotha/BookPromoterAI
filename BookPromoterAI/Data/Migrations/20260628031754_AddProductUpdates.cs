using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookPromoterAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedItems = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedItems = table.Column<string>(type: "TEXT", nullable: false),
                    AddedItems = table.Column<string>(type: "TEXT", nullable: false),
                    SocialPostText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EmailedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EmailsSent = table.Column<int>(type: "INTEGER", nullable: false),
                    EmailsFailed = table.Column<int>(type: "INTEGER", nullable: false),
                    SocialPostsSent = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUpdates", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductUpdates");
        }
    }
}
