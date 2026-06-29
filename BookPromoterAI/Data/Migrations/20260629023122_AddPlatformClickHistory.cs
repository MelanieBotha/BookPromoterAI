using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookPromoterAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformClickHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlatformClickHistory",
                table: "Books",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlatformClickHistory",
                table: "Books");
        }
    }
}
