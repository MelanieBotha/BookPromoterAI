using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace BookPromoterAI;

// =====================================================================
// DATABASE-BACKED APP STORE
//
// Drop-in replacement for the in-memory AppStore. All data is persisted
// to SQLite via Entity Framework Core. The public API is identical to
// the in-memory version so all routes and pages work unchanged.
// =====================================================================
class AppStoreDb
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IHttpContextAccessor _http;
    private readonly AppSettings _settings;
    private DbUser? _cachedUser;

    private const string SessionEmailKey = "LoggedInEmail";
    private const string SessionOwnerKey = "OwnerUnlocked";

    public AppStoreDb(IDbContextFactory<AppDbContext> dbFactory, IHttpContextAccessor http, AppSettings settings)
    {
        _dbFactory = dbFactory;
        _http = http;
        _settings = settings;
    }

    private ISession? Session => _http.HttpContext?.Session;

    public string? LoggedInEmail
    {
        get => Session?.GetString(SessionEmailKey);
        private set
        {
            if (Session is null) return;
            if (string.IsNullOrWhiteSpace(value)) Session.Remove(SessionEmailKey);
            else Session.SetString(SessionEmailKey, value);
            _cachedUser = null;
        }
    }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(LoggedInEmail);

    public bool OwnerUnlocked
    {
        get => Session?.GetString(SessionOwnerKey) == "1";
        private set
        {
            if (Session is null) return;
            if (value) Session.SetString(SessionOwnerKey, "1");
            else Session.Remove(SessionOwnerKey);
        }
    }

    public string? CurrentUserCode => GetCurrentUser()?.UserCode;

    // ── DB helper ──────────────────────────────────────────────────────
    private AppDbContext Db() => _dbFactory.CreateDbContext();

    // ── Current user ───────────────────────────────────────────────────
    private DbUser? GetCurrentUser()
    {
        if (string.IsNullOrWhiteSpace(LoggedInEmail)) return null;
        if (_cachedUser is not null && _cachedUser.Email == LoggedInEmail) return _cachedUser;
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        if (user is not null && RevokeExpiredAccessIfNeeded(user, db))
            user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        _cachedUser = user;
        return user;
    }

    private int CurrentUserId() => GetCurrentUser()?.Id ?? 0;

    private void ClearUserCache() => _cachedUser = null;

    public AppSettings Settings => _settings;
    public bool IsBillingConfigured => _settings.IsBillingConfigured;
    public bool IsStripeConfigured => _settings.IsStripeConfigured;
    public bool IsPayPalConfigured => _settings.IsPayPalConfigured;

    public DbUser? GetCurrentDbUser() => GetCurrentUser();

    public DbSubscriptionPlan? GetDbPlan(string planId)
    {
        using var db = Db();
        return db.SubscriptionPlans.Find(planId);
    }

    public string? CurrentPaymentProvider => GetCurrentUser()?.PaymentProvider;
    public string? CurrentBillingStatus => GetCurrentUser()?.BillingStatus;
    public bool HasProviderSubscription
    {
        get
        {
            var u = GetCurrentUser();
            return u is not null && (
                !string.IsNullOrWhiteSpace(u.StripeSubscriptionId) ||
                !string.IsNullOrWhiteSpace(u.PayPalSubscriptionId));
        }
    }

    // ── Plans ──────────────────────────────────────────────────────────
    public List<SubscriptionPlan> Plans
    {
        get
        {
            using var db = Db();
            return db.SubscriptionPlans.AsNoTracking()
                .ToList()
                .OrderBy(p => p.MonthlyFee)
                .Select(ToModel)
                .ToList();
        }
    }

    public SubscriptionPlan? CurrentPlan
    {
        get
        {
            var planId = GetCurrentUser()?.CurrentPlanId;
            return planId is null ? null : Plans.FirstOrDefault(p => p.Id == planId);
        }
    }

    public bool HasCustomerAccess => GetCurrentUser()?.HasCustomerAccess ?? false;
    public string AccessType => GetCurrentUser()?.AccessType ?? "No Access Selected";
    public DateTime? AccessEndsAt => GetCurrentUser()?.AccessEndsAt;
    public string? CurrentPlanId => GetCurrentUser()?.CurrentPlanId;
    public bool IsCancelled => GetCurrentUser()?.IsCancelled ?? false;
    public DateTime? SubscriptionEndsAt => GetCurrentUser()?.SubscriptionEndsAt;

    public bool IsTrialPreview => AccessType == "Free Trial";
    public bool CanSeeTeamAccess => (CurrentPlan?.HasTeamAccess ?? false) || IsTrialPreview || AccessType == "Lifetime Free (Publisher)";
    public bool CanSeeAdvancedAnalytics => (CurrentPlan?.HasAdvancedAnalytics ?? false) || IsTrialPreview || AccessType == "Lifetime Free (Publisher)";
    public bool CanSeeMultiClient => (CurrentPlan?.HasMultiClient ?? false) || IsTrialPreview || AccessType == "Lifetime Free (Publisher)";
    public bool HasTeamAccess => CurrentPlan?.HasTeamAccess ?? false;
    public bool HasAdvancedAnalytics => CurrentPlan?.HasAdvancedAnalytics ?? false;
    public bool HasMultiClient => (CurrentPlan?.HasMultiClient ?? false) || AccessType == "Lifetime Free (Publisher)";

    public string AccessStatusText
    {
        get
        {
            if (!HasCustomerAccess) return "No plan selected yet. Use an access code or choose a subscription to unlock the app.";
            if (AccessType == "Free Trial" && AccessEndsAt is not null)
            {
                var days = Math.Max(0, (int)Math.Ceiling((AccessEndsAt.Value - DateTime.UtcNow).TotalDays));
                return $"You are on an access code period. {days} day(s) remaining.";
            }
            if (AccessType == "Lifetime Free (Publisher)") return "You have lifetime free access at the Publisher tier. No billing required.";
            var plan = CurrentPlan;
            return plan is not null ? $"You are on the {plan.Name} plan. Monthly fee: ${plan.MonthlyFee:0.00}." : "You are on a paid plan.";
        }
    }

    // ── Books ──────────────────────────────────────────────────────────
    public List<Book> Books
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.Books.Include(b => b.Links)
                .Where(b => b.UserId == uid)
                .AsNoTracking().ToList()
                .Select(ToModel).ToList();
        }
    }

    public Book AddBook(Book book)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var dbBook = ToDb(book, uid);
        db.Books.Add(dbBook);
        db.SaveChanges();
        book.Id = dbBook.Id;
        MatchBookToClient(book);
        if (book.ClientId.HasValue)
        {
            dbBook.ClientId = book.ClientId;
            db.SaveChanges();
        }
        return book;
    }

    public void UpdateBook(Book book)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var existing = db.Books.Include(b => b.Links).FirstOrDefault(b => b.Id == book.Id && b.UserId == uid);
        if (existing is null) return;
        existing.Title = book.Title;
        existing.AuthorName = book.AuthorName;
        existing.Genre = book.Genre;
        existing.Description = book.Description;
        existing.CoverImageUrl = book.CoverImageUrl;
        existing.CoverSourceUrl = book.CoverSourceUrl;
        existing.PostVariantSeed = book.PostVariantSeed;
        existing.MonthlyClicks = book.MonthlyClicks;
        existing.ClickHistoryJson = JsonSerializer.Serialize(book.ClickHistory);
        MatchBookToClient(book);
        existing.ClientId = book.ClientId;
        db.BookLinks.RemoveRange(existing.Links);
        existing.Links = book.Links.Select(l => new DbBookLink { StoreName = l.StoreName, Url = l.Url }).ToList();
        db.SaveChanges();
    }

    public void RemoveBook(int id)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var book = db.Books.FirstOrDefault(b => b.Id == id && b.UserId == uid);
        if (book is not null) { db.Books.Remove(book); db.SaveChanges(); }
    }

    public Book? FindBook(int id)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var b = db.Books.Include(x => x.Links).FirstOrDefault(x => x.Id == id && x.UserId == uid);
        return b is null ? null : ToModel(b);
    }

    public Book? RecordClick(string trackingCode)
    {
        using var db = Db();
        var b = db.Books.Include(x => x.Links).FirstOrDefault(x => x.TrackingCode == trackingCode);
        if (b is null) return null;
        b.MonthlyClicks++;
        var key = DateTime.UtcNow.ToString("yyyy-MM");
        var hist = JsonSerializer.Deserialize<Dictionary<string, int>>(b.ClickHistoryJson) ?? [];
        hist[key] = hist.TryGetValue(key, out var prev) ? prev + 1 : 1;
        b.ClickHistoryJson = JsonSerializer.Serialize(hist);
        db.SaveChanges();
        return ToModel(b);
    }

    // ── Social Accounts ────────────────────────────────────────────────
    public List<SocialAccount> SocialAccounts
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.SocialAccounts.Where(a => a.UserId == uid).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public SocialAccount AddSocialAccount(SocialAccount account)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var dbAcc = new DbSocialAccount { UserId = uid, Platform = account.Platform, DisplayName = account.DisplayName, Handle = account.Handle, IsConnected = account.IsConnected, ConnectedViaOAuth = account.ConnectedViaOAuth, AccessToken = account.SimulatedAccessToken };
        db.SocialAccounts.Add(dbAcc);
        db.SaveChanges();
        account.Id = dbAcc.Id;
        return account;
    }

    public void UpdateSocialAccount(SocialAccount account)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var existing = db.SocialAccounts.FirstOrDefault(a => a.Id == account.Id && a.UserId == uid);
        if (existing is null) return;
        existing.Platform = account.Platform; existing.DisplayName = account.DisplayName; existing.Handle = account.Handle;
        db.SaveChanges();
    }

    public SocialAccount? FindSocialAccount(int id)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var a = db.SocialAccounts.FirstOrDefault(x => x.Id == id && x.UserId == uid);
        return a is null ? null : ToModel(a);
    }

    public void RemoveSocialAccount(int id)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var acc = db.SocialAccounts.FirstOrDefault(a => a.Id == id && a.UserId == uid);
        if (acc is not null) { db.SocialAccounts.Remove(acc); db.SaveChanges(); }
    }

    // ── Schedules ──────────────────────────────────────────────────────
    public List<SocialSchedule> Schedules
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.SocialSchedules.Where(s => s.UserId == uid).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public void AddSchedule(SocialSchedule schedule)
    {
        var uid = CurrentUserId();
        using var db = Db();
        if (db.SocialSchedules.Any(s => s.UserId == uid && s.Platform == schedule.Platform)) return;
        db.SocialSchedules.Add(new DbSocialSchedule { UserId = uid, Platform = schedule.Platform, PostsPerWeek = schedule.PostsPerWeek, RequiresApproval = schedule.RequiresApproval, AutoPostEnabled = schedule.AutoPostEnabled });
        db.SaveChanges();
    }

    public void SaveSchedules(List<SocialSchedule> schedules)
    {
        var uid = CurrentUserId();
        using var db = Db();
        foreach (var s in schedules)
        {
            var existing = db.SocialSchedules.FirstOrDefault(x => x.UserId == uid && x.Platform == s.Platform);
            if (existing is null) { db.SocialSchedules.Add(new DbSocialSchedule { UserId = uid, Platform = s.Platform, PostsPerWeek = s.PostsPerWeek, RequiresApproval = s.RequiresApproval, AutoPostEnabled = s.AutoPostEnabled }); }
            else { existing.PostsPerWeek = s.PostsPerWeek; existing.RequiresApproval = s.RequiresApproval; existing.AutoPostEnabled = s.AutoPostEnabled; }
        }
        db.SaveChanges();
    }

    public void RemoveSchedule(string platform)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var s = db.SocialSchedules.FirstOrDefault(x => x.UserId == uid && x.Platform.ToLower() == platform.ToLower());
        if (s is not null) { db.SocialSchedules.Remove(s); db.SaveChanges(); }
    }

    // ── Generated Ads ──────────────────────────────────────────────────
    public List<GeneratedAd> GeneratedAds
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.GeneratedAds.Where(a => a.UserId == uid).OrderByDescending(a => a.GeneratedAt).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public GeneratedAd RecordGeneratedAd(Book book, string platform, string postText)
    {
        var uid = CurrentUserId();
        var now = DateTime.UtcNow;
        var weekNum = System.Globalization.ISOWeek.GetWeekOfYear(now);
        var weekYear = System.Globalization.ISOWeek.GetYear(now);
        var weekStart = System.Globalization.ISOWeek.ToDateTime(weekYear, weekNum, DayOfWeek.Monday);
        var weekLabel = $"Week {weekNum} \u2013 {weekStart:MMM d} to {weekStart.AddDays(6):MMM d}";
        using var db = Db();
        var ad = new DbGeneratedAd { UserId = uid, BookId = book.Id, BookTitle = book.Title, CoverImageUrl = book.CoverImageUrl, Platform = platform, PostText = postText, GeneratedAt = now, WeekNumber = weekNum, WeekYear = weekYear, WeekLabel = weekLabel };
        db.GeneratedAds.Add(ad);
        db.SaveChanges();
        return ToModel(ad);
    }

    public void ApproveAd(int id)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var ad = db.GeneratedAds.FirstOrDefault(a => a.Id == id && a.UserId == uid);
        if (ad is not null) { ad.ApprovedForPosting = true; db.SaveChanges(); }
    }

    public int? RegenerateAd(int adId, PostGenerator generator, string baseUrl)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var ad = db.GeneratedAds.FirstOrDefault(a => a.Id == adId && a.UserId == uid);
        if (ad is null) return null;
        var book = db.Books.Include(b => b.Links).FirstOrDefault(b => b.Id == ad.BookId && b.UserId == uid);
        if (book is null) return null;

        book.PostVariantSeed++;
        var model = ToModel(book);
        var purchaseUrl = PostBranding.PrimaryPurchaseUrl(model) ?? "";
        var text = generator.Generate(model, ad.Platform, purchaseUrl, book.PostVariantSeed, baseUrl);
        ad.PostText = text;
        ad.GeneratedAt = DateTime.UtcNow;
        ad.ApprovedForPosting = false;
        db.SaveChanges();
        return adId;
    }

    public (string Subject, string Body, int BookId, string? Error) BuildMailingListDraft(MailingListEmailGenerator generator, string baseUrl, int? bookId = null, bool regenerate = false)
    {
        var books = Books;
        if (books.Count == 0) return ("", "", 0, "Add at least one book before generating a mailing list email.");

        var book = bookId is int id ? books.FirstOrDefault(b => b.Id == id) : null;
        book ??= books[Math.Abs(DateTime.UtcNow.GetHashCode()) % books.Count];

        if (regenerate)
        {
            book.PostVariantSeed++;
            UpdateBook(book);
        }

        var trackingUrl = PostBranding.PrimaryPurchaseUrl(book) ?? "";
        var (subject, body) = generator.Generate(book, trackingUrl, book.PostVariantSeed);
        return (subject, body, book.Id, null);
    }

    public List<GeneratedAd> GenerateWeeklyPosts(PostGenerator generator, string baseUrl)
    {
        var newAds = new List<GeneratedAd>();
        var books = Books;
        var schedules = Schedules;
        if (books.Count == 0 || schedules.Count == 0) return newAds;

        var now = DateTime.UtcNow;
        var currentWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
        var currentYear = System.Globalization.ISOWeek.GetYear(now);

        var uid = CurrentUserId();
        using var db = Db();
        var postsThisWeekByPlatform = db.GeneratedAds
            .Where(a => a.UserId == uid && a.WeekNumber == currentWeek && a.WeekYear == currentYear)
            .GroupBy(a => a.Platform)
            .ToDictionary(g => g.Key, g => g.Count());

        var bookIndex = 0;
        foreach (var schedule in schedules.Where(s => s.PostsPerWeek > 0))
        {
            var existingCount = postsThisWeekByPlatform.GetValueOrDefault(schedule.Platform, 0);
            var needed = schedule.PostsPerWeek - existingCount;
            if (needed <= 0) continue;
            for (var i = 0; i < needed; i++)
            {
                var book = books[bookIndex % books.Count];
                book.PostVariantSeed++;
                UpdateBook(book);
                var purchaseUrl = PostBranding.PrimaryPurchaseUrl(book) ?? "";
                var text = generator.Generate(book, schedule.Platform, purchaseUrl, book.PostVariantSeed, baseUrl);
                newAds.Add(RecordGeneratedAd(book, schedule.Platform, text));
                bookIndex++;
            }
        }
        return newAds;
    }

    // ── Posting Log ────────────────────────────────────────────────────
    public List<PostingLogEntry> PostingLog
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.PostingLog.Where(l => l.UserId == uid).OrderByDescending(l => l.AttemptedAt).Take(50).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    // ── Team ───────────────────────────────────────────────────────────
    public List<TeamMember> TeamMembers
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.TeamMembers.Where(t => t.UserId == uid).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public (TeamMember? Member, string Message) AddTeamMember(string email, string role)
    {
        var uid = CurrentUserId();
        var cleanEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanEmail)) return (null, "Enter an email address.");
        using var db = Db();
        if (db.TeamMembers.Any(t => t.UserId == uid && t.Email == cleanEmail)) return (null, "That person has already been invited.");
        var inviteCode = GenerateCode("INVITE-");
        var dbMember = new DbTeamMember { UserId = uid, Email = cleanEmail, Role = string.IsNullOrWhiteSpace(role) ? "Editor" : role, InviteCode = inviteCode, InvitedAt = DateTime.UtcNow };
        db.TeamMembers.Add(dbMember);
        db.SaveChanges();
        return (ToModel(dbMember), $"Invitation sent to {cleanEmail}.");
    }

    public void RemoveTeamMember(string email)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var m = db.TeamMembers.FirstOrDefault(t => t.UserId == uid && t.Email.ToLower() == email.ToLower());
        if (m is not null) { db.TeamMembers.Remove(m); db.SaveChanges(); }
    }

    // ── Clients ────────────────────────────────────────────────────────
    public List<Client> Clients
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.Clients.Where(c => c.UserId == uid).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public Client AddClient(string name, string contactEmail, string notes)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var c = new DbClient { UserId = uid, Name = name, ContactEmail = contactEmail, Notes = notes };
        db.Clients.Add(c);
        db.SaveChanges();
        return ToModel(c);
    }

    public void RemoveClient(int id)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var c = db.Clients.FirstOrDefault(x => x.Id == id && x.UserId == uid);
        if (c is not null) { db.Clients.Remove(c); db.SaveChanges(); }
    }

    // ── Mailing List ───────────────────────────────────────────────────
    public List<MailingListSubscriber> MailingListSubscribers
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.MailingListSubscribers.Where(s => s.UserId == uid).OrderByDescending(s => s.SubscribedAt).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public List<MailingListCampaign> MailingListCampaigns
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.MailingListCampaigns.Where(c => c.UserId == uid).OrderByDescending(c => c.SentAt).Take(20).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public MailingListCampaign? GetMailingListCampaign(int id)
    {
        var uid = CurrentUserId();
        if (uid == 0) return null;
        using var db = Db();
        return db.MailingListCampaigns.AsNoTracking().FirstOrDefault(c => c.Id == id && c.UserId == uid) is { } row
            ? ToModel(row)
            : null;
    }

    public (bool Success, string Message) AddMailingListSubscriber(string email, string name, string source = "Manual")
    {
        var uid = CurrentUserId();
        if (uid == 0) return (false, "Not logged in.");
        var cleanEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanEmail) || !cleanEmail.Contains('@')) return (false, "Enter a valid email address.");
        using var db = Db();
        if (db.MailingListSubscribers.Any(s => s.UserId == uid && s.Email == cleanEmail)) return (false, "That email is already on your mailing list.");
        db.MailingListSubscribers.Add(new DbMailingListSubscriber { UserId = uid, Email = cleanEmail, Name = name.Trim(), SubscribedAt = DateTime.UtcNow, Source = source });
        db.SaveChanges();
        return (true, $"Added {cleanEmail} to your mailing list.");
    }

    public void RemoveMailingListSubscriber(int id)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var sub = db.MailingListSubscribers.FirstOrDefault(s => s.Id == id && s.UserId == uid);
        if (sub is not null) { db.MailingListSubscribers.Remove(sub); db.SaveChanges(); }
    }

    public (bool Success, string Message, string? AuthorLabel) SubscribeToMailingListByUserCode(string userCode, string email, string name)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanEmail) || !cleanEmail.Contains('@')) return (false, "Enter a valid email address.", null);
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.UserCode == userCode.Trim());
        if (user is null) return (false, "This signup link is not valid.", null);
        if (db.MailingListSubscribers.Any(s => s.UserId == user.Id && s.Email == cleanEmail)) return (false, "You're already subscribed to this mailing list.", user.Email);
        db.MailingListSubscribers.Add(new DbMailingListSubscriber { UserId = user.Id, Email = cleanEmail, Name = name.Trim(), SubscribedAt = DateTime.UtcNow, Source = "Signup" });
        db.SaveChanges();
        return (true, "You're subscribed! Watch your inbox for updates.", user.Email);
    }

    public string? GetAuthorEmailByUserCode(string userCode)
    {
        using var db = Db();
        return db.Users.AsNoTracking().FirstOrDefault(u => u.UserCode == userCode.Trim())?.Email;
    }

    public async Task<(int Sent, int Failed, string Message)> SendMailingListCampaignAsync(
        string subject, string body, string apiKey, string senderEmail, string senderName, string fromDisplayName)
    {
        var uid = CurrentUserId();
        if (uid == 0) return (0, 0, "Not logged in.");
        if (string.IsNullOrWhiteSpace(subject)) return (0, 0, "Enter an email subject.");
        if (string.IsNullOrWhiteSpace(body)) return (0, 0, "Enter a message to send.");

        using var db = Db();
        var subscribers = await db.MailingListSubscribers.Where(s => s.UserId == uid).ToListAsync();
        if (subscribers.Count == 0) return (0, 0, "Your mailing list is empty. Add subscribers first.");

        var sent = 0;
        var failed = 0;
        foreach (var sub in subscribers)
        {
            var ok = await EmailService.SendMailingListEmail(sub.Email, sub.Name, subject, body, fromDisplayName, apiKey, senderEmail, senderName);
            if (ok) sent++; else failed++;
        }

        db.MailingListCampaigns.Add(new DbMailingListCampaign
        {
            UserId = uid,
            Subject = subject.Trim(),
            Body = body.Trim(),
            RecipientCount = sent,
            FailedCount = failed,
            SentAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var message = failed == 0
            ? $"Email sent to {sent} subscriber(s)."
            : $"Sent to {sent} subscriber(s). {failed} failed.";
        return (sent, failed, message);
    }

    // ── Promo Codes ────────────────────────────────────────────────────
    public List<PromoCode> PromoCodes
    {
        get
        {
            using var db = Db();
            return db.PromoCodes.AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public void SeedPromoCodes()
    {
        using var db = Db();
        if (db.PromoCodes.Any()) return; // already seeded
        db.PromoCodes.Add(new DbPromoCode { Code = GenerateCode("LIFETIME-"), IsLifetimeFree = true, FreeTrialDays = 0 });
        db.SaveChanges();
    }

    public PromoCode GenerateAccessCode(string? email = null)
    {
        using var db = Db();
        var code = GenerateCode("ACCESS-");
        var p = new DbPromoCode { Code = code, FreeTrialDays = 30, IntendedRecipientEmail = email, IsLifetimeFree = false };
        db.PromoCodes.Add(p); db.SaveChanges();
        return ToModel(p);
    }

    public PromoCode GenerateLifetimeCode(string? email = null)
    {
        using var db = Db();
        var code = GenerateCode("LIFETIME-");
        var p = new DbPromoCode { Code = code, IsLifetimeFree = true, IntendedRecipientEmail = email };
        db.PromoCodes.Add(p); db.SaveChanges();
        return ToModel(p);
    }

    // ── Feedback ───────────────────────────────────────────────────────
    public List<FeedbackEntry> FeedbackEntries
    {
        get
        {
            using var db = Db();
            return db.FeedbackEntries.OrderByDescending(f => f.SubmittedAt).AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public FeedbackEntry AddFeedback(string email, string category, string message)
    {
        using var db = Db();
        var entry = new DbFeedbackEntry { Email = email.Trim(), Category = string.IsNullOrWhiteSpace(category) ? "Suggestion" : category, Message = message.Trim(), SubmittedAt = DateTime.UtcNow, ThankYouEmail = EmailService.GenerateThankYouEmail(email, category, message) };
        db.FeedbackEntries.Add(entry); db.SaveChanges();
        return ToModel(entry);
    }

    public void ToggleFeedbackInvestigated(int id)
    {
        using var db = Db();
        var f = db.FeedbackEntries.Find(id);
        if (f is not null) { f.Investigated = !f.Investigated; db.SaveChanges(); }
    }

    // ── Auth ───────────────────────────────────────────────────────────
    public PromoRedeemResult Register(string email, string password)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanEmail) || string.IsNullOrWhiteSpace(password)) return new(false, "Enter an email and password.");
        using var db = Db();
        if (db.Users.Any(u => u.Email == cleanEmail)) return new(false, "An account with that email already exists.");
        var code = GenerateCode("BPA-");
        var user = new DbUser { Email = cleanEmail, PasswordHash = PasswordHasher.Hash(password), UserCode = code };
        db.Users.Add(user); db.SaveChanges();
        LoggedInEmail = cleanEmail;
        return new(true, $"Account created. Your account code is {code}.");
    }

    public PromoRedeemResult Login(string email, string password)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == cleanEmail);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash)) return new(false, "Incorrect email or password.");
        if (PasswordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.Hash(password);
            db.SaveChanges();
        }
        LoggedInEmail = cleanEmail;
        ClearUserCache();
        CheckAccessExpiry();
        return new(true, "Logged in.");
    }

    public void Logout()
    {
        LoggedInEmail = null;
        OwnerUnlocked = false;
        ClearUserCache();
    }

    public void DeleteAccount()
    {
        if (LoggedInEmail is null) return;
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        if (user is not null) { db.Users.Remove(user); db.SaveChanges(); }
        Logout();
    }

    // ── Payment method ─────────────────────────────────────────────────
    public PaymentMethod? CurrentPaymentMethod
    {
        get
        {
            var u = GetCurrentUser();
            if (u?.CardLast4 is null && string.IsNullOrWhiteSpace(u?.BankIban) && string.IsNullOrWhiteSpace(u?.BankName)) return null;
            return ToPaymentMethod(u!);
        }
    }

    public string SavePaymentMethod(PaymentMethodInput input)
    {
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        var message = SavePaymentMethodForUser(user, input);
        if (message == "Payment method saved.") db.SaveChanges();
        return message;
    }

    static string SavePaymentMethodForUser(DbUser? user, PaymentMethodInput input)
    {
        if (user is null) return "Not logged in.";
        var error = ValidatePaymentInput(input);
        if (error is not null) return error;

        ApplyPaymentInput(user, input);
        return "Payment method saved.";
    }

    static string? ValidatePaymentInput(PaymentMethodInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Country)) return "Select your country.";
        if (input.Country.Equals("OTHER", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(input.CountryOther))
            return "Enter your country name.";

        if (input.PaymentType == PaymentOptions.TypeBank)
        {
            if (string.IsNullOrWhiteSpace(input.CardholderName)) return "Enter the account holder name.";
            if (string.IsNullOrWhiteSpace(input.BankName)) return "Enter your bank name.";
            var iban = input.Iban.Replace(" ", "").ToUpperInvariant();
            var accountDigits = new string(input.AccountNumber.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(iban) && string.IsNullOrWhiteSpace(accountDigits))
                return "Enter an IBAN or account number.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(input.CardholderName)) return "Enter the name on your card.";
        var cardDigits = new string(input.CardNumber.Where(char.IsDigit).ToArray());
        if (cardDigits.Length < 13) return "Enter a valid card number.";
        if (string.IsNullOrWhiteSpace(input.CardExpiry)) return "Enter the card expiry (MM/YY).";
        return null;
    }

    static void ApplyPaymentInput(DbUser user, PaymentMethodInput input)
    {
        user.PaymentCountry = input.ResolvedCountry;
        user.PaymentRegion = input.Region;
        user.PaymentType = input.PaymentType == PaymentOptions.TypeBank ? PaymentOptions.TypeBank : PaymentOptions.TypeCard;

        if (user.PaymentType == PaymentOptions.TypeBank)
        {
            user.CardholderName = input.CardholderName.Trim();
            user.BankName = input.BankName.Trim();
            user.BankRoutingOrSortCode = input.RoutingOrSortCode.Trim();
            user.BankIban = input.Iban.Replace(" ", "").ToUpperInvariant();
            var accountDigits = new string(input.AccountNumber.Where(char.IsDigit).ToArray());
            user.CardLast4 = accountDigits.Length >= 4 ? accountDigits[^4..] : accountDigits;
            user.CardExpiry = null;
            return;
        }

        var digits = new string(input.CardNumber.Where(char.IsDigit).ToArray());
        user.CardholderName = input.CardholderName.Trim();
        user.CardLast4 = digits.Length >= 4 ? digits[^4..] : digits;
        user.CardExpiry = input.CardExpiry.Trim();
        user.BankName = null;
        user.BankRoutingOrSortCode = null;
        user.BankIban = null;
    }

    static PaymentMethod ToPaymentMethod(DbUser u) => new()
    {
        PaymentType = u.PaymentType ?? PaymentOptions.TypeCard,
        Country = u.PaymentCountry ?? "",
        Region = u.PaymentRegion ?? "",
        CardholderName = u.CardholderName ?? "",
        Last4 = u.CardLast4 ?? "",
        Expiry = u.CardExpiry ?? "",
        BankName = u.BankName ?? "",
        RoutingOrSortCode = u.BankRoutingOrSortCode ?? "",
        Iban = u.BankIban ?? ""
    };

    // ── Subscription ───────────────────────────────────────────────────
    public PromoRedeemResult RedeemPromoCode(string email, string code)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        if (user is null) return new(false, "Not logged in.");
        if (db.Subscriptions.Any(s => s.UserId == user.Id)) return new(false, "This account already has an active subscription.");
        var promo = db.PromoCodes.FirstOrDefault(p => p.Code == code.Trim().ToUpperInvariant());
        if (promo is null) return new(false, "That access code is not valid.");
        if (promo.IsRedeemed) return new(false, "That access code has already been used.");
        if (!string.IsNullOrWhiteSpace(promo.IntendedRecipientEmail) && !promo.IntendedRecipientEmail.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase)) return new(false, "That access code was not assigned to this email.");

        promo.IsRedeemed = true; promo.RedeemedByEmail = cleanEmail; promo.RedeemedAt = DateTime.UtcNow;
        var sub = new DbSubscription { UserId = user.Id, Email = cleanEmail, TrialStartedAt = DateTime.UtcNow, TrialEndsAt = promo.IsLifetimeFree ? DateTime.MaxValue : DateTime.UtcNow.AddDays(promo.FreeTrialDays), PromoCodeUsed = promo.Code };
        db.Subscriptions.Add(sub);

        if (promo.IsLifetimeFree) { user.HasCustomerAccess = true; user.AccessType = "Lifetime Free (Publisher)"; user.CurrentPlanId = "publisher"; }
        else { user.HasCustomerAccess = true; user.AccessType = "Free Trial"; user.AccessEndsAt = sub.TrialEndsAt; user.CurrentPlanId = "professional"; }

        db.PromoCodes.Add(new DbPromoCode { Code = promo.IsLifetimeFree ? GenerateCode("LIFETIME-") : GenerateCode("ACCESS-"), IsLifetimeFree = promo.IsLifetimeFree, FreeTrialDays = promo.FreeTrialDays });
        db.SaveChanges();
        ClearUserCache();
        return new(true, promo.IsLifetimeFree ? "Lifetime free access activated!" : $"Your {promo.FreeTrialDays}-day access code is active.");
    }

    public PromoRedeemResult StartPaidSubscription(string email, string planId, PaymentMethodInput payment)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        using var db = Db();
        var plan = db.SubscriptionPlans.Find(planId);
        if (plan is null) return new(false, "Choose a valid plan.");
        var user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        if (user is null) return new(false, "Not logged in.");

        var paymentError = ValidatePaymentInput(payment);
        if (paymentError is not null) return new(false, paymentError);

        ApplyPaymentInput(user, payment);
        db.Subscriptions.Add(new DbSubscription { UserId = user.Id, Email = cleanEmail, TrialStartedAt = DateTime.UtcNow, TrialEndsAt = DateTime.UtcNow.AddMonths(1), PromoCodeUsed = $"Paid ({plan.Name})" });
        user.HasCustomerAccess = true; user.AccessType = $"{plan.Name} Subscription"; user.CurrentPlanId = plan.Id; user.IsCancelled = false; user.SubscriptionEndsAt = DateTime.UtcNow.AddMonths(1);
        db.SaveChanges();
        ClearUserCache();
        return new(true, $"{plan.Name} subscription started.");
    }

    public PromoRedeemResult ChangePlan(string planId)
    {
        using var db = Db();
        var plan = db.SubscriptionPlans.Find(planId);
        if (plan is null) return new(false, "Choose a valid plan.");
        var user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        if (user is null) return new(false, "Not logged in.");
        user.AccessType = $"{plan.Name} Subscription"; user.CurrentPlanId = plan.Id; user.HasCustomerAccess = true; user.IsCancelled = false;
        db.SaveChanges();
        ClearUserCache();
        return new(true, $"Switched to the {plan.Name} plan.");
    }

    public PromoRedeemResult CancelSubscription()
    {
        if (CurrentPlan is null) return new(false, "No active paid subscription to cancel.");
        if (IsCancelled) return new(false, "Already set to cancel.");
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        if (user is null) return new(false, "Not logged in.");
        user.IsCancelled = true; user.SubscriptionEndsAt ??= DateTime.UtcNow.AddMonths(1);
        if (string.IsNullOrWhiteSpace(user.StripeSubscriptionId) && string.IsNullOrWhiteSpace(user.PayPalSubscriptionId))
        {
            user.PaymentType = null; user.PaymentCountry = null; user.PaymentRegion = null;
            user.CardholderName = null; user.CardLast4 = null; user.CardExpiry = null;
            user.BankName = null; user.BankRoutingOrSortCode = null; user.BankIban = null;
        }
        user.BillingStatus = "cancelled";
        db.SaveChanges();
        ClearUserCache();
        return new(true, $"Subscription cancelled. Access continues until {user.SubscriptionEndsAt:MMMM d, yyyy}.");
    }

    public bool ActivatePaidSubscriptionFromProvider(
        int userId, string planId, string provider,
        string? stripeCustomerId, string? stripeSubscriptionId, string? paypalSubscriptionId,
        DateTime periodEnd, string paymentSummary)
    {
        using var db = Db();
        var plan = db.SubscriptionPlans.Find(planId);
        if (plan is null) return false;
        var user = db.Users.Find(userId);
        if (user is null) return false;

        user.HasCustomerAccess = true;
        user.AccessType = $"{plan.Name} Subscription";
        user.CurrentPlanId = plan.Id;
        user.IsCancelled = false;
        user.SubscriptionEndsAt = periodEnd;
        user.PaymentProvider = provider;
        user.BillingStatus = "active";
        user.PaymentType = provider;
        user.CardholderName = paymentSummary;

        if (!string.IsNullOrWhiteSpace(stripeCustomerId)) user.StripeCustomerId = stripeCustomerId;
        if (!string.IsNullOrWhiteSpace(stripeSubscriptionId)) user.StripeSubscriptionId = stripeSubscriptionId;
        if (!string.IsNullOrWhiteSpace(paypalSubscriptionId)) user.PayPalSubscriptionId = paypalSubscriptionId;

        if (!db.Subscriptions.Any(s => s.UserId == userId && s.PromoCodeUsed.StartsWith("Paid")))
        {
            db.Subscriptions.Add(new DbSubscription
            {
                UserId = userId,
                Email = user.Email,
                TrialStartedAt = DateTime.UtcNow,
                TrialEndsAt = periodEnd,
                PromoCodeUsed = $"Paid ({plan.Name})"
            });
        }

        db.SaveChanges();
        if (user.Email == LoggedInEmail) ClearUserCache();
        return true;
    }

    public void MarkSubscriptionPendingCancel(string externalSubscriptionId, string provider, DateTime periodEnd)
    {
        using var db = Db();
        var user = FindUserByExternalSubscription(db, externalSubscriptionId, provider);
        if (user is null) return;
        user.IsCancelled = true;
        user.SubscriptionEndsAt = periodEnd;
        user.BillingStatus = "active";
        db.SaveChanges();
        if (user.Email == LoggedInEmail) ClearUserCache();
    }

    public void MarkSubscriptionCancelledByProvider(string externalSubscriptionId, string provider, DateTime? periodEnd)
    {
        using var db = Db();
        var user = FindUserByExternalSubscription(db, externalSubscriptionId, provider);
        if (user is null) return;
        user.IsCancelled = true;
        user.SubscriptionEndsAt = periodEnd ?? DateTime.UtcNow;
        user.BillingStatus = "cancelled";
        db.SaveChanges();
        if (user.Email == LoggedInEmail) ClearUserCache();
    }

    public void SyncProviderSubscription(string externalSubscriptionId, string provider, string status, DateTime periodEnd)
    {
        using var db = Db();
        var user = FindUserByExternalSubscription(db, externalSubscriptionId, provider);
        if (user is null) return;
        user.SubscriptionEndsAt = periodEnd;
        user.BillingStatus = status;
        user.IsCancelled = false;
        user.HasCustomerAccess = status is "active" or "past_due";
        db.SaveChanges();
        if (user.Email == LoggedInEmail) ClearUserCache();
    }

    public void SetBillingStatus(string externalSubscriptionId, string provider, string status)
    {
        using var db = Db();
        var user = FindUserByExternalSubscription(db, externalSubscriptionId, provider);
        if (user is null) return;
        user.BillingStatus = status;
        db.SaveChanges();
        if (user.Email == LoggedInEmail) ClearUserCache();
    }

    static DbUser? FindUserByExternalSubscription(AppDbContext db, string externalId, string provider) =>
        provider == "stripe"
            ? db.Users.FirstOrDefault(u => u.StripeSubscriptionId == externalId)
            : db.Users.FirstOrDefault(u => u.PayPalSubscriptionId == externalId);

    public void CheckAccessExpiry()
    {
        var user = GetCurrentUser();
        if (user is null) return;
        using var db = Db();
        if (RevokeExpiredAccessIfNeeded(user, db))
            ClearUserCache();
    }

    private static bool RevokeExpiredAccessIfNeeded(DbUser user, AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var revoke = false;

        if (user.AccessType == "Free Trial" && user.AccessEndsAt is DateTime trialEnd && now >= trialEnd)
            revoke = true;

        if (user.IsCancelled && user.SubscriptionEndsAt is DateTime subEnd && now >= subEnd)
            revoke = true;

        if (!revoke) return false;

        user.HasCustomerAccess = false;
        user.AccessType = "No Access Selected";
        user.CurrentPlanId = null;
        user.AccessEndsAt = null;
        user.IsCancelled = false;
        user.SubscriptionEndsAt = null;
        db.SaveChanges();
        return true;
    }

    // ── Password reset ─────────────────────────────────────────────────
    public string? GeneratePasswordResetToken(string email)
    {
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == email.Trim().ToLowerInvariant());
        if (user is null) return null;
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        user.ResetToken = token; user.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        db.SaveChanges();
        return token;
    }

    public PromoRedeemResult ResetPassword(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6) return new(false, "Password must be at least 6 characters.");
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.ResetToken == token);
        if (user is null || user.ResetTokenExpiresAt < DateTime.UtcNow) return new(false, "Reset link expired or invalid.");
        user.PasswordHash = PasswordHasher.Hash(newPassword); user.ResetToken = null; user.ResetTokenExpiresAt = null;
        db.SaveChanges();
        return new(true, "Password updated. You can now log in.");
    }

    // ── Owner ──────────────────────────────────────────────────────────
    public bool UnlockOwner(string pin)
    {
        if (string.IsNullOrWhiteSpace(_settings.OwnerPin)) return false;
        OwnerUnlocked = pin.Trim() == _settings.OwnerPin.Trim();
        return OwnerUnlocked;
    }

    public void UpdatePlanPrice(string planId, decimal fee)
    {
        using var db = Db();
        var plan = db.SubscriptionPlans.Find(planId);
        if (plan is not null) { plan.MonthlyFee = Math.Max(0, fee); db.SaveChanges(); }
    }

    public void UpdatePlanPaymentIds(string planId, string? stripePriceId, string? paypalPlanId)
    {
        using var db = Db();
        var plan = db.SubscriptionPlans.Find(planId);
        if (plan is null) return;
        plan.StripePriceId = stripePriceId?.Trim() ?? "";
        plan.PayPalPlanId = paypalPlanId?.Trim() ?? "";
        db.SaveChanges();
    }

    public void SaveOwnerStripeConnectAccountId(string accountId)
    {
        using var db = Db();
        var row = db.OwnerPayoutSettings.Find(1);
        if (row is null)
        {
            row = new DbOwnerPayoutSettings { Id = 1 };
            db.OwnerPayoutSettings.Add(row);
        }
        row.StripeConnectAccountId = accountId.Trim();
        db.SaveChanges();
    }

    public OwnerPayoutSettings GetOwnerPayoutSettings()
    {
        using var db = Db();
        var row = db.OwnerPayoutSettings.Find(1);
        return row is null ? new OwnerPayoutSettings() : ToModel(row);
    }

    public string SaveOwnerPayoutSettings(OwnerPayoutSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.AccountHolderName)) return "Enter the account holder name.";
        if (string.IsNullOrWhiteSpace(settings.BankName)) return "Enter the bank name.";
        if (string.IsNullOrWhiteSpace(settings.AccountNumber)) return "Enter the account number.";

        using var db = Db();
        var row = db.OwnerPayoutSettings.Find(1);
        if (row is null)
        {
            row = new DbOwnerPayoutSettings { Id = 1 };
            db.OwnerPayoutSettings.Add(row);
        }

        row.AccountHolderName = settings.AccountHolderName.Trim();
        row.BankName = settings.BankName.Trim();
        row.AccountType = string.IsNullOrWhiteSpace(settings.AccountType) ? "Checking" : settings.AccountType.Trim();
        row.RoutingOrSortCode = settings.RoutingOrSortCode.Trim();
        row.AccountNumber = new string(settings.AccountNumber.Where(char.IsDigit).ToArray());
        row.Iban = settings.Iban.Trim().Replace(" ", "").ToUpperInvariant();
        row.Notes = settings.Notes.Trim();
        db.SaveChanges();
        return "Payout bank account saved.";
    }

    // ── Limits ─────────────────────────────────────────────────────────
    public string? CheckBookLimit()
    {
        var plan = CurrentPlan;
        if (plan?.BookLimit is int l && Books.Count >= l) return $"You've reached the {l}-book limit on the {plan.Name} plan.";
        return null;
    }

    public string? CheckSocialAccountLimit()
    {
        var plan = CurrentPlan;
        if (plan?.SocialAccountLimit is int l && SocialAccounts.Count >= l) return $"You've reached the {l}-account limit on the {plan.Name} plan.";
        return null;
    }

    // ── Auto-posting (background scheduler) ───────────────────────────
    public async Task<int> RunDuePostsAsync(SocialPostingService postingService)
    {
        var count = 0;
        var now = DateTime.UtcNow;
        var currentWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
        using var db = Db();

        var schedules = await db.SocialSchedules.Where(s => s.AutoPostEnabled && s.PostsPerWeek > 0).ToListAsync();
        foreach (var schedule in schedules)
        {
            if (schedule.WeekTrackerStart != currentWeek) { schedule.WeekTrackerStart = currentWeek; schedule.PostsSentThisWeek = 0; }
            if (schedule.PostsSentThisWeek >= schedule.PostsPerWeek) continue;
            var hoursBetween = (24.0 * 7) / schedule.PostsPerWeek;
            if (schedule.LastPostedAt is DateTime last && (now - last).TotalHours < hoursBetween) continue;
            var account = await db.SocialAccounts.FirstOrDefaultAsync(a => a.UserId == schedule.UserId && a.Platform == schedule.Platform);
            if (account is null) continue;
            var candidate = await db.GeneratedAds.Where(a => a.UserId == schedule.UserId && a.Platform == schedule.Platform && a.PostStatus == "Pending" && (!schedule.RequiresApproval || a.ApprovedForPosting)).OrderBy(a => a.GeneratedAt).FirstOrDefaultAsync();
            if (candidate is null) continue;
            var result = await postingService.PostAsync(ToModel(account), candidate.PostText);
            candidate.PostStatus = result.Success ? "Posted" : "Failed"; candidate.PostedAt = result.Success ? now : null; candidate.PostError = result.Success ? null : result.Message;
            db.PostingLog.Add(new DbPostingLogEntry { UserId = schedule.UserId, GeneratedAdId = candidate.Id, Platform = schedule.Platform, BookTitle = candidate.BookTitle, Success = result.Success, Message = result.Message, AttemptedAt = now });
            if (result.Success) { schedule.LastPostedAt = now; schedule.PostsSentThisWeek++; count++; }
        }
        await db.SaveChangesAsync();
        return count;
    }

    // ── Client matching ────────────────────────────────────────────────
    public void MatchBookToClient(Book book)
    {
        var clients = Clients;
        var match = clients.FirstOrDefault(c => c.Name.Equals(book.AuthorName, StringComparison.OrdinalIgnoreCase));
        book.ClientId = match?.Id;
    }

    // ── Helpers ────────────────────────────────────────────────────────
    private static string GenerateCode(string prefix)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var suffix = new char[6];
        for (var i = 0; i < 6; i++) suffix[i] = chars[Random.Shared.Next(chars.Length)];
        return prefix + new string(suffix);
    }

    // ── Model converters ───────────────────────────────────────────────
    static Book ToModel(DbBook b) => new() { Id = b.Id, Title = b.Title, AuthorName = b.AuthorName, Genre = b.Genre, Description = b.Description, CoverImageUrl = b.CoverImageUrl, CoverSourceUrl = b.CoverSourceUrl, TrackingCode = b.TrackingCode, MonthlyClicks = b.MonthlyClicks, PostVariantSeed = b.PostVariantSeed, ClientId = b.ClientId, Links = b.Links.Select(l => new BookLink { StoreName = l.StoreName, Url = l.Url }).ToList(), ClickHistory = JsonSerializer.Deserialize<Dictionary<string, int>>(b.ClickHistoryJson) ?? [] };
    static DbBook ToDb(Book b, int uid) => new() { UserId = uid, Title = b.Title, AuthorName = b.AuthorName, Genre = b.Genre, Description = b.Description, CoverImageUrl = b.CoverImageUrl, CoverSourceUrl = b.CoverSourceUrl, TrackingCode = b.TrackingCode, MonthlyClicks = b.MonthlyClicks, PostVariantSeed = b.PostVariantSeed, ClientId = b.ClientId, ClickHistoryJson = JsonSerializer.Serialize(b.ClickHistory), Links = b.Links.Select(l => new DbBookLink { StoreName = l.StoreName, Url = l.Url }).ToList() };
    static SocialAccount ToModel(DbSocialAccount a) => new() { Id = a.Id, Platform = a.Platform, DisplayName = a.DisplayName, Handle = a.Handle, IsConnected = a.IsConnected, ConnectedViaOAuth = a.ConnectedViaOAuth, SimulatedAccessToken = a.AccessToken };
    static SocialSchedule ToModel(DbSocialSchedule s) => new() { Platform = s.Platform, PostsPerWeek = s.PostsPerWeek, RequiresApproval = s.RequiresApproval, AutoPostEnabled = s.AutoPostEnabled, LastPostedAt = s.LastPostedAt, PostsSentThisWeek = s.PostsSentThisWeek, WeekTrackerStart = s.WeekTrackerStart };
    static GeneratedAd ToModel(DbGeneratedAd a) => new() { Id = a.Id, BookId = a.BookId, BookTitle = a.BookTitle, CoverImageUrl = a.CoverImageUrl, Platform = a.Platform, PostText = a.PostText, GeneratedAt = a.GeneratedAt, WeekNumber = a.WeekNumber, WeekYear = a.WeekYear, WeekLabel = a.WeekLabel, PostStatus = a.PostStatus, PostedAt = a.PostedAt, PostError = a.PostError, ApprovedForPosting = a.ApprovedForPosting };
    static PostingLogEntry ToModel(DbPostingLogEntry l) => new() { Id = l.Id, GeneratedAdId = l.GeneratedAdId, Platform = l.Platform, BookTitle = l.BookTitle, Success = l.Success, Message = l.Message, AttemptedAt = l.AttemptedAt };
    static TeamMember ToModel(DbTeamMember t) => new() { Email = t.Email, Role = t.Role, InviteCode = t.InviteCode, Accepted = t.Accepted, InvitedAt = t.InvitedAt };
    static Client ToModel(DbClient c) => new() { Id = c.Id, Name = c.Name, ContactEmail = c.ContactEmail, Notes = c.Notes };
    static PromoCode ToModel(DbPromoCode p) => new() { Code = p.Code, FreeTrialDays = p.FreeTrialDays, IntendedRecipientEmail = p.IntendedRecipientEmail, IsRedeemed = p.IsRedeemed, RedeemedByEmail = p.RedeemedByEmail, RedeemedAt = p.RedeemedAt, IsLifetimeFree = p.IsLifetimeFree };
    static FeedbackEntry ToModel(DbFeedbackEntry f) => new() { Id = f.Id, Email = f.Email, Category = f.Category, Message = f.Message, SubmittedAt = f.SubmittedAt, Investigated = f.Investigated, ThankYouEmail = f.ThankYouEmail };
    static SubscriptionPlan ToModel(DbSubscriptionPlan p) => new()
    {
        Id = p.Id, Name = p.Name, MonthlyFee = p.MonthlyFee, BookLimit = p.BookLimit,
        SocialAccountLimit = p.SocialAccountLimit, AiPostsPerMonth = p.AiPostsPerMonth,
        HasTeamAccess = p.HasTeamAccess, HasAdvancedAnalytics = p.HasAdvancedAnalytics,
        HasMultiClient = p.HasMultiClient,
        Features = JsonSerializer.Deserialize<List<string>>(p.FeaturesJson) ?? [],
        StripePriceId = p.StripePriceId,
        PayPalPlanId = p.PayPalPlanId
    };
    static OwnerPayoutSettings ToModel(DbOwnerPayoutSettings s) => new()
    {
        AccountHolderName = s.AccountHolderName, BankName = s.BankName, AccountType = s.AccountType,
        RoutingOrSortCode = s.RoutingOrSortCode, AccountNumber = s.AccountNumber, Iban = s.Iban,
        Notes = s.Notes, StripeConnectAccountId = s.StripeConnectAccountId
    };
    static MailingListSubscriber ToModel(DbMailingListSubscriber s) => new() { Id = s.Id, Email = s.Email, Name = s.Name, SubscribedAt = s.SubscribedAt, Source = s.Source };
    static MailingListCampaign ToModel(DbMailingListCampaign c) => new() { Id = c.Id, Subject = c.Subject, Body = c.Body, RecipientCount = c.RecipientCount, FailedCount = c.FailedCount, SentAt = c.SentAt };
}
