using System.Data;
using Microsoft.EntityFrameworkCore;

namespace BookPromoterAI;

static class DatabaseInitializer
{
    public static void ApplyMigrations(AppDbContext db)
    {
        if (!db.Database.GetAppliedMigrations().Any() && LegacySchemaExists(db))
        {
            // Legacy EnsureCreated DB — claim only InitialCreate, then apply later migrations.
            db.Database.ExecuteSqlRaw(
                "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                "20260626211235_InitialCreate",
                "8.0.0");
        }

        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex) when (IsDuplicateColumnError(ex))
        {
            // Column already added by RepairMissingColumns on a prior deploy — continue with repairs.
            db.Database.ExecuteSqlRaw(
                "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                "20260629023122_AddPlatformClickHistory",
                "8.0.0");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DatabaseInitializer] EF Migrate skipped: {ex.Message}");
        }

        LogRepairStep("RepairMissingTables", () => RepairMissingTables(db));
        LogRepairStep("RepairMissingColumns", () => RepairMissingColumns(db));
        RepairPlanDefaults(db);
    }

    static void LogRepairStep(string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DatabaseInitializer] {step} failed: {ex}");
            throw;
        }
    }

    static bool IsDuplicateColumnError(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static void RepairMissingTables(AppDbContext db)
    {
        if (!TableExists(db, "ProductUpdates"))
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "ProductUpdates" (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "Version" TEXT NOT NULL,
                    "Title" TEXT NOT NULL,
                    "UpdatedItems" TEXT NOT NULL,
                    "CreatedItems" TEXT NOT NULL,
                    "AddedItems" TEXT NOT NULL,
                    "SocialPostText" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "EmailedAt" TEXT NULL,
                    "EmailsSent" INTEGER NOT NULL,
                    "EmailsFailed" INTEGER NOT NULL,
                    "SocialPostsSent" INTEGER NOT NULL
                );
                """);

            db.Database.ExecuteSqlRaw(
                "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                "20260628031754_AddProductUpdates",
                "8.0.0");
        }

        if (!TableExists(db, "MailingListSettings"))
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "MailingListSettings" (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "UserId" INTEGER NOT NULL,
                    "ListKind" TEXT NOT NULL DEFAULT 'Author',
                    "EmailsPerWeek" INTEGER NOT NULL DEFAULT 0,
                    "AutoSendEnabled" INTEGER NOT NULL DEFAULT 0,
                    "RequiresApproval" INTEGER NOT NULL DEFAULT 1,
                    "LastSentAt" TEXT NULL,
                    "EmailsSentThisWeek" INTEGER NOT NULL DEFAULT 0,
                    "WeekTrackerStart" INTEGER NOT NULL DEFAULT 0,
                    "PendingSubject" TEXT NOT NULL DEFAULT '',
                    "PendingBody" TEXT NOT NULL DEFAULT '',
                    "PendingBookId" INTEGER NULL,
                    "DraftGeneratedAt" TEXT NULL,
                    "PendingApproved" INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
                );
                """);
        }

        if (!TableExists(db, "TikTokVideos"))
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "TikTokVideos" (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "UserId" INTEGER NOT NULL,
                    "BookId" INTEGER NOT NULL,
                    "BookTitle" TEXT NOT NULL,
                    "Title" TEXT NOT NULL,
                    "Caption" TEXT NOT NULL,
                    "VideoUrl" TEXT NOT NULL,
                    "Status" TEXT NOT NULL,
                    "ErrorMessage" TEXT NULL,
                    "TikTokPublishId" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "PostedAt" TEXT NULL,
                    FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
                );
                """);
            db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_TikTokVideos_UserId" ON "TikTokVideos" ("UserId");""");
            db.Database.ExecuteSqlRaw(
                "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                "20260706044301_AddTikTokVideos",
                "8.0.0");
        }
    }

    static void RepairMailingListSettingsIndex(AppDbContext db)
    {
        if (!TableExists(db, "MailingListSettings")) return;
        AddColumnIfMissing(db, "MailingListSettings", "ListKind",
            """ALTER TABLE "MailingListSettings" ADD COLUMN "ListKind" TEXT NOT NULL DEFAULT 'Author'""");
        if (!ColumnExists(db, "MailingListSettings", "ListKind")) return;

        db.Database.ExecuteSqlRaw("""UPDATE "MailingListSettings" SET "ListKind" = 'Author' WHERE "ListKind" IS NULL OR "ListKind" = ''""");
        try
        {
            db.Database.ExecuteSqlRaw("""
                DELETE FROM "MailingListSettings"
                WHERE "Id" NOT IN (
                    SELECT MIN("Id") FROM "MailingListSettings" GROUP BY "UserId", "ListKind"
                );
                """);
            db.Database.ExecuteSqlRaw("""DROP INDEX IF EXISTS "IX_MailingListSettings_UserId";""");
            db.Database.ExecuteSqlRaw(
                """CREATE UNIQUE INDEX IF NOT EXISTS "IX_MailingListSettings_UserId_ListKind" ON "MailingListSettings" ("UserId", "ListKind");""");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DatabaseInitializer] MailingListSettings index repair skipped: {ex.Message}");
        }
    }

    static bool TableExists(AppDbContext db, string table)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
        var param = cmd.CreateParameter();
        param.ParameterName = "$name";
        param.Value = table;
        cmd.Parameters.Add(param);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    static void RepairPlanDefaults(AppDbContext db)
    {
        var starter = db.SubscriptionPlans.Find("starter");
        if (starter is not null && starter.MonthlyFee != 4.99m)
        {
            starter.MonthlyFee = 4.99m;
            db.SaveChanges();
        }
    }

    static void RepairMissingColumns(AppDbContext db)
    {
        AddColumnIfMissing(db, "Users", "PaymentType", """ALTER TABLE "Users" ADD COLUMN "PaymentType" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "PaymentCountry", """ALTER TABLE "Users" ADD COLUMN "PaymentCountry" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "PaymentRegion", """ALTER TABLE "Users" ADD COLUMN "PaymentRegion" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "BankName", """ALTER TABLE "Users" ADD COLUMN "BankName" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "BankRoutingOrSortCode", """ALTER TABLE "Users" ADD COLUMN "BankRoutingOrSortCode" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "BankIban", """ALTER TABLE "Users" ADD COLUMN "BankIban" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "StripeCustomerId", """ALTER TABLE "Users" ADD COLUMN "StripeCustomerId" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "StripeSubscriptionId", """ALTER TABLE "Users" ADD COLUMN "StripeSubscriptionId" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "PayPalSubscriptionId", """ALTER TABLE "Users" ADD COLUMN "PayPalSubscriptionId" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "PaymentProvider", """ALTER TABLE "Users" ADD COLUMN "PaymentProvider" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "BillingStatus", """ALTER TABLE "Users" ADD COLUMN "BillingStatus" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "TermsAcceptedAt", """ALTER TABLE "Users" ADD COLUMN "TermsAcceptedAt" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "TermsAcceptedVersion", """ALTER TABLE "Users" ADD COLUMN "TermsAcceptedVersion" TEXT NULL""");
        AddColumnIfMissing(db, "SubscriptionPlans", "StripePriceId", """ALTER TABLE "SubscriptionPlans" ADD COLUMN "StripePriceId" TEXT NULL""");
        AddColumnIfMissing(db, "SubscriptionPlans", "PayPalPlanId", """ALTER TABLE "SubscriptionPlans" ADD COLUMN "PayPalPlanId" TEXT NULL""");
        AddColumnIfMissing(db, "OwnerPayoutSettings", "StripeConnectAccountId", """ALTER TABLE "OwnerPayoutSettings" ADD COLUMN "StripeConnectAccountId" TEXT NULL""");
        AddColumnIfMissing(db, "MailingListSubscribers", "UnsubscribeToken", """ALTER TABLE "MailingListSubscribers" ADD COLUMN "UnsubscribeToken" TEXT NOT NULL DEFAULT ''""");
        AddColumnIfMissing(db, "Books", "PlatformClickHistory", """ALTER TABLE "Books" ADD COLUMN "PlatformClickHistory" TEXT NOT NULL DEFAULT '{{}}'""");
        db.Database.ExecuteSqlRaw("""UPDATE "Books" SET "PlatformClickHistory" = '{{}}' WHERE "PlatformClickHistory" IS NULL OR "PlatformClickHistory" = ''""");
        AddColumnIfMissing(db, "SocialAccounts", "RefreshToken", """ALTER TABLE "SocialAccounts" ADD COLUMN "RefreshToken" TEXT NULL""");
        AddColumnIfMissing(db, "SocialAccounts", "ExternalAccountId", """ALTER TABLE "SocialAccounts" ADD COLUMN "ExternalAccountId" TEXT NULL""");
        AddColumnIfMissing(db, "SocialAccounts", "AccountKind", """ALTER TABLE "SocialAccounts" ADD COLUMN "AccountKind" TEXT NOT NULL DEFAULT 'Author'""");
        db.Database.ExecuteSqlRaw("""UPDATE "SocialAccounts" SET "AccountKind" = 'Author' WHERE "AccountKind" IS NULL OR "AccountKind" = ''""");
        AddColumnIfMissing(db, "SocialSchedules", "ScheduleKind", """ALTER TABLE "SocialSchedules" ADD COLUMN "ScheduleKind" TEXT NOT NULL DEFAULT 'Author'""");
        db.Database.ExecuteSqlRaw("""UPDATE "SocialSchedules" SET "ScheduleKind" = 'Author' WHERE "ScheduleKind" IS NULL OR "ScheduleKind" = ''""");
        AddColumnIfMissing(db, "MailingListSubscribers", "ListKind", """ALTER TABLE "MailingListSubscribers" ADD COLUMN "ListKind" TEXT NOT NULL DEFAULT 'Author'""");
        AddColumnIfMissing(db, "MailingListCampaigns", "ListKind", """ALTER TABLE "MailingListCampaigns" ADD COLUMN "ListKind" TEXT NOT NULL DEFAULT 'Author'""");
        AddColumnIfMissing(db, "MailingListSettings", "ListKind", """ALTER TABLE "MailingListSettings" ADD COLUMN "ListKind" TEXT NOT NULL DEFAULT 'Author'""");
        if (TableExists(db, "MailingListSubscribers") && ColumnExists(db, "MailingListSubscribers", "ListKind"))
            db.Database.ExecuteSqlRaw("""UPDATE "MailingListSubscribers" SET "ListKind" = 'Author' WHERE "ListKind" IS NULL OR "ListKind" = ''""");
        if (TableExists(db, "MailingListCampaigns") && ColumnExists(db, "MailingListCampaigns", "ListKind"))
            db.Database.ExecuteSqlRaw("""UPDATE "MailingListCampaigns" SET "ListKind" = 'Author' WHERE "ListKind" IS NULL OR "ListKind" = ''""");
        if (TableExists(db, "MailingListSettings") && ColumnExists(db, "MailingListSettings", "ListKind"))
            db.Database.ExecuteSqlRaw("""UPDATE "MailingListSettings" SET "ListKind" = 'Author' WHERE "ListKind" IS NULL OR "ListKind" = ''""");
        AddColumnIfMissing(db, "MailingListSettings", "PendingNewReleaseBookId", """ALTER TABLE "MailingListSettings" ADD COLUMN "PendingNewReleaseBookId" INTEGER NULL""");
        AddColumnIfMissing(db, "PostingLog", "LogKind", """ALTER TABLE "PostingLog" ADD COLUMN "LogKind" TEXT NOT NULL DEFAULT 'Author'""");
        AddColumnIfMissing(db, "GeneratedAds", "ScheduledPostAt", """ALTER TABLE "GeneratedAds" ADD COLUMN "ScheduledPostAt" TEXT NULL""");
        db.Database.ExecuteSqlRaw("""UPDATE "PostingLog" SET "LogKind" = 'Author' WHERE "LogKind" IS NULL OR "LogKind" = ''""");
        db.Database.ExecuteSqlRaw("""UPDATE "PostingLog" SET "LogKind" = 'Brand' WHERE "BookTitle" = 'BookPromoter AI' OR "BookTitle" LIKE 'Update v%'""");
        MigrateOwnerBrandMailingListSubscribers(db);
        OwnerBrandDataMigrator.MigrateToPrimaryOwner(db);
        OwnerBrandDataMigrator.DemoteFormerOwnerAccounts(db);
        PruneOrphanedGeneratedAds(db);
        PruneOrphanAuthorSchedules(db);
        RepairMailingListSettingsIndex(db);
    }

    static void PruneOrphanAuthorSchedules(AppDbContext db)
    {
        if (!TableExists(db, "SocialSchedules") || !TableExists(db, "SocialAccounts")) return;

        foreach (var userId in db.SocialSchedules
            .Where(s => s.ScheduleKind == SocialScheduleKinds.Author || s.ScheduleKind == "")
            .Select(s => s.UserId)
            .Distinct()
            .ToList())
        {
            var connected = db.SocialAccounts
                .Where(a => a.UserId == userId && a.IsConnected
                    && (a.AccountKind == SocialAccountKinds.Author || a.AccountKind == ""))
                .AsNoTracking()
                .ToList();
            var orphanSchedules = db.SocialSchedules
                .Where(s => s.UserId == userId && (s.ScheduleKind == SocialScheduleKinds.Author || s.ScheduleKind == ""))
                .AsEnumerable()
                .Where(s => !connected.Any(a => PostLimits.PlatformsMatch(a.Platform, s.Platform)))
                .ToList();
            foreach (var orphan in orphanSchedules)
            {
                var ads = db.GeneratedAds.Where(a => a.UserId == userId).AsEnumerable()
                    .Where(a => PostLimits.PlatformsMatch(a.Platform, orphan.Platform))
                    .ToList();
                if (ads.Count > 0)
                    db.GeneratedAds.RemoveRange(ads);
                db.SocialSchedules.Remove(orphan);
            }
        }

        db.SaveChanges();
    }

    static void PruneOrphanedGeneratedAds(AppDbContext db)
    {
        if (!TableExists(db, "GeneratedAds") || !TableExists(db, "Books")) return;

        var bookOrphans = db.GeneratedAds.AsEnumerable()
            .Where(a => !db.Books.Any(b => b.Id == a.BookId && b.UserId == a.UserId))
            .ToList();
        if (bookOrphans.Count > 0)
            db.GeneratedAds.RemoveRange(bookOrphans);

        foreach (var userId in db.GeneratedAds.Select(a => a.UserId).Distinct().ToList())
        {
            var connectedPlatforms = db.SocialAccounts
                .Where(a => a.UserId == userId && a.IsConnected
                    && (a.AccountKind == SocialAccountKinds.Author || a.AccountKind == ""))
                .Select(a => a.Platform)
                .ToList();
            var scheduledPlatforms = db.SocialSchedules
                .Where(s => s.UserId == userId && s.PostsPerWeek > 0
                    && (s.ScheduleKind == SocialScheduleKinds.Author || s.ScheduleKind == ""))
                .Select(s => s.Platform)
                .ToList();

            var platformOrphans = db.GeneratedAds.Where(a => a.UserId == userId).AsEnumerable()
                .Where(a =>
                    !connectedPlatforms.Any(p => PostLimits.PlatformsMatch(p, a.Platform)) &&
                    !scheduledPlatforms.Any(p => PostLimits.PlatformsMatch(p, a.Platform)))
                .ToList();
            if (platformOrphans.Count > 0)
                db.GeneratedAds.RemoveRange(platformOrphans);
        }

        db.SaveChanges();
    }

    static void MigrateOwnerBrandMailingListSubscribers(AppDbContext db)
    {
        if (!TableExists(db, "MailingListSubscribers") || !ColumnExists(db, "MailingListSubscribers", "ListKind")) return;
        var owner = db.Users.AsNoTracking().FirstOrDefault(u => u.Email == OwnerAccount.NormalizedEmail);
        if (owner is null) return;
        db.Database.ExecuteSqlRaw(
            """UPDATE "MailingListSubscribers" SET "ListKind" = 'Brand' WHERE "UserId" = {0} AND ("Source" = 'Auto sync' OR "Source" = 'Signup')""",
            owner.Id);
    }

    static bool LegacySchemaExists(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    static bool ColumnExists(AppDbContext db, string table, string column)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static void AddColumnIfMissing(AppDbContext db, string table, string column, string alterSql)
    {
        if (!TableExists(db, table)) return;
        if (ColumnExists(db, table, column)) return;
        db.Database.ExecuteSqlRaw(alterSql);
    }
}
