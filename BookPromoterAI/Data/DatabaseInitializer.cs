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

        db.Database.Migrate();
        RepairMissingColumns(db);
        RepairMissingTables(db);
        RepairPlanDefaults(db);
    }

    static void RepairMissingTables(AppDbContext db)
    {
        if (TableExists(db, "ProductUpdates")) return;

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
        if (ColumnExists(db, table, column)) return;
        db.Database.ExecuteSqlRaw(alterSql);
    }
}
