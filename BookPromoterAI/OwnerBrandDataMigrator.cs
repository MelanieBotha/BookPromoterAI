namespace BookPromoterAI;

/// <summary>Moves BookPromoter AI brand rows onto the primary owner user.</summary>
static class OwnerBrandDataMigrator
{
    public static void MigrateToPrimaryOwner(AppDbContext db)
    {
        var primary = db.Users.FirstOrDefault(u => u.Email == OwnerAccount.NormalizedEmail);
        if (primary is null) return;

        var sourceUserIds = CollectBrandDataSourceUserIds(db, primary.Id);
        if (sourceUserIds.Count == 0) return;

        MigrateSocialAccounts(db, primary.Id, sourceUserIds);
        MigrateSocialSchedules(db, primary.Id, sourceUserIds);
        MigrateMailingListSettings(db, primary.Id, sourceUserIds);
        MigrateMailingListSubscribers(db, primary.Id, sourceUserIds);
        MigrateMailingListCampaigns(db, primary.Id, sourceUserIds);
        MigratePostingLog(db, primary.Id, sourceUserIds);

        db.SaveChanges();
    }

    public static void DemoteFormerOwnerAccounts(AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(OwnerAccount.LegacyPrimaryOwnerEmail)) return;

        var legacyEmail = OwnerAccount.Normalize(OwnerAccount.LegacyPrimaryOwnerEmail);
        var user = db.Users.FirstOrDefault(u => u.Email == legacyEmail);
        if (user is null || OwnerAccount.IsOwnerEmail(user.Email)) return;

        if (user.AccessType == "Owner")
            user.AccessType = user.HasCustomerAccess ? "Publisher" : "No Access Selected";

        db.SaveChanges();
    }

    static List<int> CollectBrandDataSourceUserIds(AppDbContext db, int primaryId)
    {
        var legacyEmail = string.IsNullOrWhiteSpace(OwnerAccount.LegacyPrimaryOwnerEmail)
            ? null
            : OwnerAccount.Normalize(OwnerAccount.LegacyPrimaryOwnerEmail);

        return db.Users.AsEnumerable()
            .Where(u => u.Id != primaryId && (
                OwnerAccount.IsOwnerEmail(u.Email) ||
                (legacyEmail is not null && u.Email == legacyEmail)))
            .Select(u => u.Id)
            .ToList();
    }

    static void MigrateSocialAccounts(AppDbContext db, int primaryId, List<int> sourceUserIds)
    {
        var primaryPlatforms = db.SocialAccounts
            .Where(a => a.UserId == primaryId && a.AccountKind == SocialAccountKinds.Brand)
            .Select(a => a.Platform.ToLower())
            .ToHashSet();

        foreach (var acc in db.SocialAccounts
            .Where(a => sourceUserIds.Contains(a.UserId) && a.AccountKind == SocialAccountKinds.Brand)
            .ToList())
        {
            if (primaryPlatforms.Contains(acc.Platform.ToLower()))
                db.SocialAccounts.Remove(acc);
            else
            {
                acc.UserId = primaryId;
                primaryPlatforms.Add(acc.Platform.ToLower());
            }
        }
    }

    static void MigrateSocialSchedules(AppDbContext db, int primaryId, List<int> sourceUserIds)
    {
        var primaryPlatforms = db.SocialSchedules
            .Where(s => s.UserId == primaryId && s.ScheduleKind == SocialScheduleKinds.Brand)
            .Select(s => s.Platform.ToLower())
            .ToHashSet();

        foreach (var schedule in db.SocialSchedules
            .Where(s => sourceUserIds.Contains(s.UserId) && s.ScheduleKind == SocialScheduleKinds.Brand)
            .ToList())
        {
            if (primaryPlatforms.Contains(schedule.Platform.ToLower()))
                db.SocialSchedules.Remove(schedule);
            else
            {
                schedule.UserId = primaryId;
                primaryPlatforms.Add(schedule.Platform.ToLower());
            }
        }
    }

    static void MigrateMailingListSettings(AppDbContext db, int primaryId, List<int> sourceUserIds)
    {
        var hasPrimaryBrand = db.MailingListSettings.Any(s =>
            s.UserId == primaryId && s.ListKind == MailingListKinds.Brand);

        foreach (var settings in db.MailingListSettings
            .Where(s => sourceUserIds.Contains(s.UserId) && s.ListKind == MailingListKinds.Brand)
            .ToList())
        {
            if (hasPrimaryBrand)
                db.MailingListSettings.Remove(settings);
            else
            {
                settings.UserId = primaryId;
                hasPrimaryBrand = true;
            }
        }
    }

    static void MigrateMailingListSubscribers(AppDbContext db, int primaryId, List<int> sourceUserIds)
    {
        var primaryKeys = db.MailingListSubscribers
            .Where(s => s.UserId == primaryId && s.ListKind == MailingListKinds.Brand)
            .Select(s => s.Email.ToLower())
            .ToHashSet();

        foreach (var sub in db.MailingListSubscribers
            .Where(s => sourceUserIds.Contains(s.UserId) && s.ListKind == MailingListKinds.Brand)
            .ToList())
        {
            var emailKey = sub.Email.ToLower();
            if (primaryKeys.Contains(emailKey))
                db.MailingListSubscribers.Remove(sub);
            else
            {
                sub.UserId = primaryId;
                primaryKeys.Add(emailKey);
            }
        }
    }

    static void MigrateMailingListCampaigns(AppDbContext db, int primaryId, List<int> sourceUserIds)
    {
        foreach (var campaign in db.MailingListCampaigns
            .Where(c => sourceUserIds.Contains(c.UserId) && c.ListKind == MailingListKinds.Brand)
            .ToList())
            campaign.UserId = primaryId;
    }

    static void MigratePostingLog(AppDbContext db, int primaryId, List<int> sourceUserIds)
    {
        foreach (var entry in db.PostingLog
            .Where(l => sourceUserIds.Contains(l.UserId) && l.LogKind == PostingLogKinds.Brand)
            .ToList())
            entry.UserId = primaryId;
    }
}
