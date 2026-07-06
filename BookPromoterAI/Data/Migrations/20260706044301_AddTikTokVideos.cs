using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookPromoterAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTikTokVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScheduleKind",
                table: "SocialSchedules",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountKind",
                table: "SocialAccounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountId",
                table: "SocialAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "SocialAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogKind",
                table: "PostingLog",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ListKind",
                table: "MailingListSubscribers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ListKind",
                table: "MailingListCampaigns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledPostAt",
                table: "GeneratedAds",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MailingListSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ListKind = table.Column<string>(type: "TEXT", nullable: false),
                    EmailsPerWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoSendEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EmailsSentThisWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekTrackerStart = table.Column<int>(type: "INTEGER", nullable: false),
                    PendingSubject = table.Column<string>(type: "TEXT", nullable: false),
                    PendingBody = table.Column<string>(type: "TEXT", nullable: false),
                    PendingBookId = table.Column<int>(type: "INTEGER", nullable: true),
                    PendingNewReleaseBookId = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftGeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PendingApproved = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailingListSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailingListSettings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TikTokVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    BookId = table.Column<int>(type: "INTEGER", nullable: false),
                    BookTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", nullable: false),
                    VideoUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    TikTokPublishId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TikTokVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TikTokVideos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailingListSettings_UserId",
                table: "MailingListSettings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TikTokVideos_UserId",
                table: "TikTokVideos",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailingListSettings");

            migrationBuilder.DropTable(
                name: "TikTokVideos");

            migrationBuilder.DropColumn(
                name: "ScheduleKind",
                table: "SocialSchedules");

            migrationBuilder.DropColumn(
                name: "AccountKind",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalAccountId",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "SocialAccounts");

            migrationBuilder.DropColumn(
                name: "LogKind",
                table: "PostingLog");

            migrationBuilder.DropColumn(
                name: "ListKind",
                table: "MailingListSubscribers");

            migrationBuilder.DropColumn(
                name: "ListKind",
                table: "MailingListCampaigns");

            migrationBuilder.DropColumn(
                name: "ScheduledPostAt",
                table: "GeneratedAds");
        }
    }
}
