namespace BookPromoterAI;

class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Description { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";
    public string CoverSourceUrl { get; set; } = "";
    public List<BookLink> Links { get; set; } = [];
    public string TrackingCode { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public int MonthlyClicks { get; set; }
    public int PostVariantSeed { get; set; }

    // When multi-client management is enabled, each book is linked to the
    // client whose name matches the book's AuthorName. This is set
    // automatically when a book is added or edited — the app looks for a
    // Client whose Name matches the book's AuthorName (case-insensitive).
    public int? ClientId { get; set; }

    // Monthly click history keyed by "yyyy-MM" for trend charts.
    // Updated by RecordClick in AppStore alongside MonthlyClicks.
    public Dictionary<string, int> ClickHistory { get; set; } = [];

    // Monthly clicks by platform (Facebook, X, etc.) keyed by "yyyy-MM".
    public Dictionary<string, Dictionary<string, int>> PlatformClickHistory { get; set; } = [];
}

class BookLink
{
    public string StoreName { get; set; } = "";
    public string Url { get; set; } = "";
}

class SocialSchedule
{
    public string Platform { get; set; } = "";
    public int PostsPerWeek { get; set; }
    public bool RequiresApproval { get; set; }

    // Auto-posting fields: tracks when this platform last posted and how
    // many posts have gone out so far this week, so the scheduler knows
    // when the next post is due without needing exact per-day slots.
    public bool AutoPostEnabled { get; set; }
    public string ScheduleKind { get; set; } = SocialScheduleKinds.Author;
    public DateTime? LastPostedAt { get; set; }
    public int PostsSentThisWeek { get; set; }
    public int WeekTrackerStart { get; set; } // ISO week number tracker resets PostsSentThisWeek
}

class SocialAccount
{
    public int Id { get; set; }
    public string Platform { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Handle { get; set; } = "";
    public bool IsConnected { get; set; }
    public bool ConnectedViaOAuth { get; set; }
    public string AccountKind { get; set; } = SocialAccountKinds.Author;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ExternalAccountId { get; set; }
    public string? SimulatedAccessToken { get; set; }

    public bool IsLiveConnection =>
        IsConnected
        && !string.IsNullOrWhiteSpace(AccessToken)
        && !AccessToken.StartsWith("SIMULATED-", StringComparison.Ordinal)
        && (!PostLimits.IsBluesky(Platform) || !string.IsNullOrWhiteSpace(ExternalAccountId))
        && (!PostLimits.IsX(Platform) || !string.IsNullOrWhiteSpace(ExternalAccountId))
        && (!PostLimits.IsLinkedIn(Platform) || !string.IsNullOrWhiteSpace(ExternalAccountId))
        && (!PostLimits.IsFacebook(Platform) || !string.IsNullOrWhiteSpace(ExternalAccountId))
        && (!PostLimits.IsInstagram(Platform) || !string.IsNullOrWhiteSpace(ExternalAccountId));
}

class PromoCode
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

class OwnerPlanMember
{
    public string Email { get; set; } = "";
    public string AccessType { get; set; } = "";
    public string BillingLabel { get; set; } = "";
    public bool IsCancelled { get; set; }
    public DateTime? AccessEndsAt { get; set; }
}

class Subscription
{
    public string Email { get; set; } = "";
    public DateTime TrialStartedAt { get; set; } = DateTime.UtcNow;
    public DateTime TrialEndsAt { get; set; } = DateTime.UtcNow.AddDays(30);
    public string PromoCodeUsed { get; set; } = "";
    public int DaysRemaining => Math.Max(0, (int)Math.Ceiling((TrialEndsAt - DateTime.UtcNow).TotalDays));
}

class SubscriptionPlan
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
    public List<string> Features { get; set; } = [];
    public string? StripePriceId { get; set; }
    public string? PayPalPlanId { get; set; }

    public string BookLimitText => BookLimit?.ToString() ?? "Unlimited";
    public string SocialAccountLimitText => SocialAccountLimit?.ToString() ?? "Unlimited";
    public string AiPostsPerMonthText => AiPostsPerMonth?.ToString() ?? "Unlimited";

    public int? MaxWeeklyPosts => AiPostsPerMonth is int monthly
        ? Math.Max(1, (int)Math.Floor(monthly / 4.33))
        : null;

    public string MaxWeeklyPostsText => MaxWeeklyPosts?.ToString() ?? "Unlimited";
}

class PaymentMethod
{
    public string PaymentType { get; set; } = "card";
    public string Country { get; set; } = "";
    public string Region { get; set; } = "";
    public string CardholderName { get; set; } = "";
    public string Last4 { get; set; } = "";
    public string Expiry { get; set; } = "";
    public string BankName { get; set; } = "";
    public string RoutingOrSortCode { get; set; } = "";
    public string Iban { get; set; } = "";

    public bool IsCard => PaymentType == PaymentOptions.TypeCard;
    public bool IsBank => PaymentType == PaymentOptions.TypeBank;

    public string Summary
    {
        get
        {
            var location = string.IsNullOrWhiteSpace(Region)
                ? Country
                : $"{Region}, {Country}";
            if (IsBank)
            {
                var accountHint = !string.IsNullOrWhiteSpace(Iban) && Iban.Length >= 4
                    ? $"IBAN ...{Iban[^4..]}"
                    : $"account ending {Last4}";
                return $"{BankName} ({accountHint}) — {location}";
            }
            return $"Card {Last4} — {CardholderName} — {location}";
        }
    }
}

class UserAccount
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string UserCode { get; set; } = "";

    // Subscription state — stored per user so logging out and back in
    // as a different user loads the correct tier, books, and data.
    public bool HasCustomerAccess { get; set; }
    public string AccessType { get; set; } = "No Access Selected";
    public DateTime? AccessEndsAt { get; set; }
    public string? CurrentPlanId { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    // Per-user data
    public List<Book> Books { get; set; } = [];
    public List<SocialSchedule> Schedules { get; set; } = [];
    public List<SocialAccount> SocialAccounts { get; set; } = [];
    public List<GeneratedAd> GeneratedAds { get; set; } = [];
    public List<Subscription> Subscriptions { get; set; } = [];
    public List<TeamMember> TeamMembers { get; set; } = [];
    public List<Client> Clients { get; set; } = [];
    public List<PostingLogEntry> PostingLog { get; set; } = [];
    public List<FeedbackEntry> FeedbackEntries { get; set; } = [];

    // Password reset token
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
    public bool ResetTokenValid => ResetToken is not null && ResetTokenExpiresAt > DateTime.UtcNow;
}

class TeamMember
{
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Editor";
    public string InviteCode { get; set; } = "";
    public bool Accepted { get; set; }
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
}

class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string Notes { get; set; } = "";
}

class GeneratedAd
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";
    public string Platform { get; set; } = "";
    public string PostText { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public int WeekNumber { get; set; }
    public int WeekYear { get; set; }
    public string WeekLabel { get; set; } = "";

    // Auto-posting status for this generated post.
    // Pending -> awaiting approval or awaiting its scheduled time
    // Posted  -> the scheduler successfully sent it (or simulated sending it)
    // Failed  -> the scheduler tried and the platform API call failed
    // Skipped -> approval required but not yet approved by the user
    public string PostStatus { get; set; } = "Pending";
    public DateTime? PostedAt { get; set; }
    public string? PostError { get; set; }
    public bool ApprovedForPosting { get; set; }
}

// One row per auto-posting attempt, shown in the Posting Activity Log
// so users can see exactly what was sent (or attempted) and when.
class PostingLogEntry
{
    public int Id { get; set; }
    public int GeneratedAdId { get; set; }
    public string Platform { get; set; } = "";
    public string BookTitle { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public DateTime AttemptedAt { get; set; }
    public string LogKind { get; set; } = PostingLogKinds.Author;
}

record PromoRedeemResult(bool Success, string Message);

class FeedbackEntry
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Category { get; set; } = "Suggestion";
    public string Message { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public bool Investigated { get; set; }
    public string ThankYouEmail { get; set; } = ""; // AI-generated thank-you response
}

// Owner-only payout destination for subscription revenue (Stripe Connect, manual transfer, etc.)
class OwnerPayoutSettings
{
    public string AccountHolderName { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountType { get; set; } = "Checking";
    public string RoutingOrSortCode { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Notes { get; set; } = "";
    public string StripeConnectAccountId { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountHolderName) &&
        !string.IsNullOrWhiteSpace(BankName) &&
        !string.IsNullOrWhiteSpace(AccountNumber);

    public string AccountNumberMasked
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AccountNumber)) return "";
            return AccountNumber.Length >= 4 ? $"****{AccountNumber[^4..]}" : "****";
        }
    }
}

class MailingListSubscriber
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime SubscribedAt { get; set; }
    public string Source { get; set; } = "Manual";
    public string UnsubscribeToken { get; set; } = "";
}

class MailingListSubscription
{
    public int Id { get; set; }
    public string ListOwnerEmail { get; set; } = "";
    public string ListOwnerDisplayName { get; set; } = "";
    public string ListKind { get; set; } = MailingListKinds.Author;
    public DateTime SubscribedAt { get; set; }
    public string Source { get; set; } = "";
}

class MailingListCampaign
{
    public int Id { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int RecipientCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime SentAt { get; set; }
}

class MailingListSettings
{
    public string ListKind { get; set; } = MailingListKinds.Author;
    public int EmailsPerWeek { get; set; }
    public bool AutoSendEnabled { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public DateTime? LastSentAt { get; set; }
    public int EmailsSentThisWeek { get; set; }
    public int WeekTrackerStart { get; set; }
    public string PendingSubject { get; set; } = "";
    public string PendingBody { get; set; } = "";
    public int? PendingBookId { get; set; }
    public int? PendingNewReleaseBookId { get; set; }
    public DateTime? DraftGeneratedAt { get; set; }
    public bool PendingApproved { get; set; }
}

class ProductUpdate
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

static class BookExtensions
{
    public static string GenreOrDefault(this Book book) =>
        string.IsNullOrWhiteSpace(book.Genre) ? "Books" : book.Genre;
}
