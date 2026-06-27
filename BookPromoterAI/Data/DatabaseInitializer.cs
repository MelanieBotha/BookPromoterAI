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
    }

    static void RepairMissingColumns(AppDbContext db)
    {
        AddColumnIfMissing(db, "Users", "PaymentType", """ALTER TABLE "Users" ADD COLUMN "PaymentType" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "PaymentCountry", """ALTER TABLE "Users" ADD COLUMN "PaymentCountry" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "PaymentRegion", """ALTER TABLE "Users" ADD COLUMN "PaymentRegion" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "BankName", """ALTER TABLE "Users" ADD COLUMN "BankName" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "BankRoutingOrSortCode", """ALTER TABLE "Users" ADD COLUMN "BankRoutingOrSortCode" TEXT NULL""");
        AddColumnIfMissing(db, "Users", "BankIban", """ALTER TABLE "Users" ADD COLUMN "BankIban" TEXT NULL""");
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
