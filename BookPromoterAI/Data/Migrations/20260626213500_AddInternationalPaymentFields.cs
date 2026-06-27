using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookPromoterAI.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260626213500_AddInternationalPaymentFields")]
    public partial class AddInternationalPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankIban",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankRoutingOrSortCode",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCountry",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentRegion",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BankIban", table: "Users");
            migrationBuilder.DropColumn(name: "BankName", table: "Users");
            migrationBuilder.DropColumn(name: "BankRoutingOrSortCode", table: "Users");
            migrationBuilder.DropColumn(name: "PaymentCountry", table: "Users");
            migrationBuilder.DropColumn(name: "PaymentRegion", table: "Users");
            migrationBuilder.DropColumn(name: "PaymentType", table: "Users");
        }
    }
}
