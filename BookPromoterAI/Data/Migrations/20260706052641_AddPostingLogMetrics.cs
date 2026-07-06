using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookPromoterAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPostingLogMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClickCount",
                table: "PostingLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalPostId",
                table: "PostingLog",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LikeCount",
                table: "PostingLog",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "MetricsFetchedAt",
                table: "PostingLog",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClickCount",
                table: "PostingLog");

            migrationBuilder.DropColumn(
                name: "ExternalPostId",
                table: "PostingLog");

            migrationBuilder.DropColumn(
                name: "LikeCount",
                table: "PostingLog");

            migrationBuilder.DropColumn(
                name: "MetricsFetchedAt",
                table: "PostingLog");
        }
    }
}
