using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace BookPromoterAI;

// =====================================================================
// DATABASE CONTEXT
//
// Uses SQLite via Entity Framework Core. The database file is created
// automatically at startup if it doesn't exist.
//
// To install the required packages, run these commands in your project
// folder (where the .csproj file is):
//
//   dotnet add package Microsoft.EntityFrameworkCore.Sqlite
//   dotnet add package Microsoft.EntityFrameworkCore.Design
//
// After adding packages, create/update the database:
//   dotnet ef migrations add InitialCreate
//   dotnet ef database update
//
// Or let the app auto-create it on startup (done in Program.cs via
// context.Database.EnsureCreated()) — simpler but no migration history.
// =====================================================================
class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DbUser> Users => Set<DbUser>();
    public DbSet<DbBook> Books => Set<DbBook>();
    public DbSet<DbBookLink> BookLinks => Set<DbBookLink>();
    public DbSet<DbSocialAccount> SocialAccounts => Set<DbSocialAccount>();
    public DbSet<DbSocialSchedule> SocialSchedules => Set<DbSocialSchedule>();
    public DbSet<DbGeneratedAd> GeneratedAds => Set<DbGeneratedAd>();
    public DbSet<DbPostingLogEntry> PostingLog => Set<DbPostingLogEntry>();
    public DbSet<DbTeamMember> TeamMembers => Set<DbTeamMember>();
    public DbSet<DbClient> Clients => Set<DbClient>();
    public DbSet<DbSubscription> Subscriptions => Set<DbSubscription>();
    public DbSet<DbPromoCode> PromoCodes => Set<DbPromoCode>();
    public DbSet<DbFeedbackEntry> FeedbackEntries => Set<DbFeedbackEntry>();
    public DbSet<DbSubscriptionPlan> SubscriptionPlans => Set<DbSubscriptionPlan>();
    public DbSet<DbOwnerPayoutSettings> OwnerPayoutSettings => Set<DbOwnerPayoutSettings>();
    public DbSet<DbBrandCommunitySettings> BrandCommunitySettings => Set<DbBrandCommunitySettings>();
    public DbSet<DbMailingListSubscriber> MailingListSubscribers => Set<DbMailingListSubscriber>();
    public DbSet<DbMailingListCampaign> MailingListCampaigns => Set<DbMailingListCampaign>();
    public DbSet<DbMailingListSettings> MailingListSettings => Set<DbMailingListSettings>();
    public DbSet<DbProductUpdate> ProductUpdates => Set<DbProductUpdate>();
    public DbSet<DbTikTokVideo> TikTokVideos => Set<DbTikTokVideo>();
    public DbSet<DbBrandClick> BrandClicks => Set<DbBrandClick>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Store JSON-serialised complex types as text columns
        model.Entity<DbBook>()
            .Property(b => b.ClickHistoryJson)
            .HasColumnName("ClickHistory");

        model.Entity<DbBook>()
            .Property(b => b.PlatformClickHistoryJson)
            .HasColumnName("PlatformClickHistory");

        model.Entity<DbSubscriptionPlan>()
            .Property(p => p.FeaturesJson)
            .HasColumnName("Features");

        // One aggregate row per (month, platform, destination) for brand-website clicks
        model.Entity<DbBrandClick>()
            .HasIndex(c => new { c.MonthKey, c.Platform, c.Destination })
            .IsUnique();

        // Seed the four subscription plans
        model.Entity<DbSubscriptionPlan>().HasData(
            new DbSubscriptionPlan { Id = "starter",      Name = "Starter",      MonthlyFee = 4.99m,  BookLimit = 5,  SocialAccountLimit = 2,  AiPostsPerMonth = 50,  HasTeamAccess = false, HasAdvancedAnalytics = false, HasMultiClient = false, FeaturesJson = """["5 books","2 social accounts","50 AI posts/month"]""" },
            new DbSubscriptionPlan { Id = "professional", Name = "Professional", MonthlyFee = 14.99m, BookLimit = 25, SocialAccountLimit = 10, AiPostsPerMonth = 100, HasTeamAccess = false, HasAdvancedAnalytics = false, HasMultiClient = false, FeaturesJson = """["25 books","10 social accounts","Unlimited scheduling","100 AI posts/month"]""" },
            new DbSubscriptionPlan { Id = "publisher",    Name = "Publisher",    MonthlyFee = 29.99m, BookLimit = null, SocialAccountLimit = null, AiPostsPerMonth = 400, HasTeamAccess = true, HasAdvancedAnalytics = true, HasMultiClient = true, FeaturesJson = """["Unlimited books","Multi-client management","Team access","Advanced analytics","400 AI posts/month"]""" },
            new DbSubscriptionPlan { Id = "agency",       Name = "Agency",       MonthlyFee = 49.99m, BookLimit = null, SocialAccountLimit = null, AiPostsPerMonth = null, HasTeamAccess = true, HasAdvancedAnalytics = true, HasMultiClient = true, FeaturesJson = """["Unlimited books","Unlimited social accounts","Multi-client management","Team access","Advanced analytics","Unlimited AI posts/month"]""" }
        );
    }
}

// =====================================================================
// DATABASE ENTITY CLASSES
// These map directly to SQLite tables. They are separate from the
// in-app model classes so EF Core doesn't need to know about
// computed properties, helper methods, etc.
// =====================================================================

class DbUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = ""; // bcrypt hash in production; plain text for now
    public string UserCode { get; set; } = "";

    // Subscription state per user
    public bool HasCustomerAccess { get; set; }
    public string AccessType { get; set; } = "No Access Selected";
    public DateTime? AccessEndsAt { get; set; }
    public string? CurrentPlanId { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }

    // Payment method (stored flat — encrypt in production)
    public string? PaymentType { get; set; }
    public string? PaymentCountry { get; set; }
    public string? PaymentRegion { get; set; }
    public string? CardholderName { get; set; }
    public string? CardLast4 { get; set; }
    public string? CardExpiry { get; set; }
    public string? BankName { get; set; }
    public string? BankRoutingOrSortCode { get; set; }
    public string? BankIban { get; set; }

    // Payment provider billing (Stripe / PayPal)
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? PayPalSubscriptionId { get; set; }
    public string? PaymentProvider { get; set; }
    public string? BillingStatus { get; set; }

    // Password reset
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }

    // Terms & Conditions acceptance
    public DateTime? TermsAcceptedAt { get; set; }
    public string? TermsAcceptedVersion { get; set; }

    public string CommunityDiscordUrl { get; set; } = "";
    public string CommunityTelegramUrl { get; set; } = "";
    public string CommunityBlogUrl { get; set; } = "";
    public string CommunityTikTokUrl { get; set; } = "";
    public string CommunityMastodonUrl { get; set; } = "";

    /// <summary>When true, Ready weekly videos are auto-sent to the connected TikTok inbox.</summary>
    public bool TikTokAutoPostEnabled { get; set; }

    // Navigation
    public List<DbBook> Books { get; set; } = [];
    public List<DbSocialAccount> SocialAccounts { get; set; } = [];
    public List<DbSocialSchedule> SocialSchedules { get; set; } = [];
    public List<DbGeneratedAd> GeneratedAds { get; set; } = [];
    public List<DbTeamMember> TeamMembers { get; set; } = [];
    public List<DbClient> Clients { get; set; } = [];
    public List<DbSubscription> Subscriptions { get; set; } = [];
    public List<DbPostingLogEntry> PostingLog { get; set; } = [];
    public List<DbTikTokVideo> TikTokVideos { get; set; } = [];
}

class DbBook
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Description { get; set; } = "";
    public string ReadAloudExcerpt { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";
    public string CoverSourceUrl { get; set; } = "";
    public string TrackingCode { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public int MonthlyClicks { get; set; }
    public int PostVariantSeed { get; set; }
    public int? ClientId { get; set; }
    public string ClickHistoryJson { get; set; } = "{}"; // JSON: {"2025-01": 42, ...}
    public string PlatformClickHistoryJson { get; set; } = "{}"; // JSON: {"2025-01": {"Facebook": 3, "X": 2}, ...}

    public DbUser? User { get; set; }
    public List<DbBookLink> Links { get; set; } = [];
}

class DbBookLink
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string StoreName { get; set; } = "";
    public string Url { get; set; } = "";
    public DbBook? Book { get; set; }
}

class DbSocialAccount
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Platform { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Handle { get; set; } = "";
    public bool IsConnected { get; set; }
    public bool ConnectedViaOAuth { get; set; }
    public string AccountKind { get; set; } = SocialAccountKinds.Author;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ExternalAccountId { get; set; } // Bluesky DID, etc.
    public DbUser? User { get; set; }
}

class DbSocialSchedule
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Platform { get; set; } = "";
    public int PostsPerWeek { get; set; }
    public bool RequiresApproval { get; set; }
    public bool AutoPostEnabled { get; set; }
    public string ScheduleKind { get; set; } = SocialScheduleKinds.Author;
    public DateTime? LastPostedAt { get; set; }
    public int PostsSentThisWeek { get; set; }
    public int WeekTrackerStart { get; set; }
    public DbUser? User { get; set; }
}

class DbGeneratedAd
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";
    public string Platform { get; set; } = "";
    public string PostText { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public DateTime? ScheduledPostAt { get; set; }
    public int WeekNumber { get; set; }
    public int WeekYear { get; set; }
    public string WeekLabel { get; set; } = "";
    public string PostStatus { get; set; } = "Pending";
    public DateTime? PostedAt { get; set; }
    public string? PostError { get; set; }
    public bool ApprovedForPosting { get; set; }
    public string PostedVia { get; set; } = "";
    public DbUser? User { get; set; }
}

class DbPostingLogEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int GeneratedAdId { get; set; }
    public string Platform { get; set; } = "";
    public string BookTitle { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public DateTime AttemptedAt { get; set; }
    public string LogKind { get; set; } = PostingLogKinds.Author;
    public string? ExternalPostId { get; set; }
    public int LikeCount { get; set; }
    public int? ClickCount { get; set; }
    public DateTime? MetricsFetchedAt { get; set; }
    public string PostDelivery { get; set; } = "";
    public DbUser? User { get; set; }
}

class DbTeamMember
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Editor";
    public string InviteCode { get; set; } = "";
    public bool Accepted { get; set; }
    public DateTime InvitedAt { get; set; }
    public DbUser? User { get; set; }
}

class DbClient
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string Notes { get; set; } = "";
    public DbUser? User { get; set; }
}

class DbSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public DateTime TrialStartedAt { get; set; }
    public DateTime TrialEndsAt { get; set; }
    public string PromoCodeUsed { get; set; } = "";
    public DbUser? User { get; set; }
}

class DbPromoCode
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int FreeTrialDays { get; set; } = 30;
    public string? IntendedRecipientEmail { get; set; }
    public bool IsRedeemed { get; set; }
    public string? RedeemedByEmail { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public bool IsLifetimeFree { get; set; }
}

class DbFeedbackEntry
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Category { get; set; } = "Suggestion";
    public string Message { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public bool Investigated { get; set; }
    public string ThankYouEmail { get; set; } = "";
}

class DbSubscriptionPlan
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal MonthlyFee { get; set; }
    public int? BookLimit { get; set; }
    public int? SocialAccountLimit { get; set; }
    public int? AiPostsPerMonth { get; set; }
    public bool HasTeamAccess { get; set; }
    public bool HasAdvancedAnalytics { get; set; }
    public bool HasMultiClient { get; set; }
    public string FeaturesJson { get; set; } = "[]";
    public string? StripePriceId { get; set; }
    public string? PayPalPlanId { get; set; }
}

class DbOwnerPayoutSettings
{
    public int Id { get; set; } = 1;
    public string AccountHolderName { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountType { get; set; } = "Checking";
    public string RoutingOrSortCode { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Notes { get; set; } = "";
    public string StripeConnectAccountId { get; set; } = "";
}

class DbBrandCommunitySettings
{
    public int Id { get; set; } = 1;
    public string DiscordUrl { get; set; } = "";
    public string TelegramUrl { get; set; } = "";
    public string MastodonUrl { get; set; } = "";
    public string BlogUrl { get; set; } = "";
}

class DbMailingListSubscriber
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime SubscribedAt { get; set; }
    public string Source { get; set; } = "Manual";
    public string UnsubscribeToken { get; set; } = "";
    public string ListKind { get; set; } = MailingListKinds.Author;
    public DbUser? User { get; set; }
}

class DbMailingListCampaign
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int RecipientCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime SentAt { get; set; }
    public string ListKind { get; set; } = MailingListKinds.Author;
    public DbUser? User { get; set; }
}

class DbMailingListSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ListKind { get; set; } = MailingListKinds.Author;
    public int EmailsPerWeek { get; set; }
    public bool AutoSendEnabled { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTime? LastSentAt { get; set; }
    public int EmailsSentThisWeek { get; set; }
    public int WeekTrackerStart { get; set; }
    public string PendingSubject { get; set; } = "";
    public string PendingBody { get; set; } = "";
    public int? PendingBookId { get; set; }
    public int? PendingNewReleaseBookId { get; set; }
    public DateTime? DraftGeneratedAt { get; set; }
    public bool PendingApproved { get; set; }
    public DbUser? User { get; set; }
}

class DbProductUpdate
{
    public int Id { get; set; }
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string UpdatedItems { get; set; } = "";
    public string CreatedItems { get; set; } = "";
    public string AddedItems { get; set; } = "";
    public string? SocialPostText { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EmailedAt { get; set; }
    public int EmailsSent { get; set; }
    public int EmailsFailed { get; set; }
    public int SocialPostsSent { get; set; }
}

// Tracks clicks to the BookPromoter AI marketing site (/start, /trial) attributed
// to the brand social platform that drove the visit. Aggregated per calendar month.
class DbBrandClick
{
    public int Id { get; set; }
    public string MonthKey { get; set; } = ""; // yyyy-MM (UTC)
    public string Platform { get; set; } = ""; // normalized platform name, e.g. "Tumblr"
    public string Destination { get; set; } = ""; // "start" or "trial"
    public int Clicks { get; set; }
}

class DbTikTokVideo
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = "";
    public string Title { get; set; } = "";
    public string Caption { get; set; } = "";
    public string VideoUrl { get; set; } = "";
    public string Status { get; set; } = TikTokVideoStatuses.Draft;
    public string? ErrorMessage { get; set; }
    public string? TikTokPublishId { get; set; }
    public string NarrationText { get; set; } = "";
    /// <summary>Author book promos vs Brand (BookPromoter AI) app promos.</summary>
    public string VideoKind { get; set; } = TikTokVideoKinds.Author;
    public int WeekNumber { get; set; }
    public int WeekYear { get; set; }
    public string WeekLabel { get; set; } = "";
    public bool AutoGenerated { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public DbUser? User { get; set; }
}
