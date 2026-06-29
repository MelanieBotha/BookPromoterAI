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
    private readonly DatabasePaths _database;
    private DbUser? _cachedUser;

    private const string SessionEmailKey = "LoggedInEmail";

    public AppStoreDb(IDbContextFactory<AppDbContext> dbFactory, IHttpContextAccessor http, AppSettings settings, DatabasePaths database)
    {
        _dbFactory = dbFactory;
        _http = http;
        _settings = settings;
        _database = database;
    }

    private ISession? Session => _http.HttpContext?.Session;

    public string? LoggedInEmail
    {
        get => Session?.GetString(SessionEmailKey);
        private set
        {
            if (Session is null) return;
            if (string.IsNullOrWhiteSpace(value)) Session.Remove(SessionEmailKey);
            else Session.SetString(SessionEmailKey, value.Trim().ToLowerInvariant());
            _cachedUser = null;
        }
    }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(LoggedInEmail);

    public bool IsOwner => OwnerAccount.IsOwnerEmail(LoggedInEmail);

    public bool HasAcceptedTerms
    {
        get
        {
            if (IsOwner) return true;
            var user = GetCurrentUser();
            return user?.TermsAcceptedAt is not null &&
                   string.Equals(user.TermsAcceptedVersion, LegalConstants.CurrentTermsVersion, StringComparison.Ordinal);
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
        if (user is not null && SyncCustomerAccessState(user, db))
            user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        _cachedUser = user;
        return user;
    }

    private int CurrentUserId() => GetCurrentUser()?.Id ?? 0;

    private void ClearUserCache() => _cachedUser = null;

    public AppSettings Settings => _settings;
    public bool IsBillingConfigured => _settings.IsBillingConfigured;
    public bool IsStripeConfigured => _settings.IsStripeConfigured;
    public string StripeSecretKeyStatus => _settings.DescribeStripeSecretKey();
    public string StripePublishableKeyStatus => _settings.DescribeStripePublishableKey();
    public string StripeWebhookSecretStatus => _settings.DescribeStripeWebhookSecret();
    public bool IsSendGridConfigured => _settings.IsSendGridConfigured;
    public string SendGridApiKeyStatus => _settings.DescribeSendGridApiKey();
    public string SendGridSenderEmailStatus => _settings.DescribeSendGridSenderEmail();
    public string PublicBaseUrlStatus => _settings.DescribePublicBaseUrl();
    public bool UsesCustomDomain => _settings.UsesCustomDomain;
    public bool ShowSoftLaunchBanner => _settings.ShowSoftLaunchBanner;
    public bool RailwayCleanupDone => _settings.RailwayCleanupDone;

    public DatabasePaths Database => _database;

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
        existing.PlatformClickHistoryJson = JsonSerializer.Serialize(book.PlatformClickHistory);
        MatchBookToClient(book);
        existing.ClientId = book.ClientId;
        db.BookLinks.RemoveRange(existing.Links);
        existing.Links = book.Links.Select(l => new DbBookLink { StoreName = l.StoreName, Url = l.Url }).ToList();
        foreach (var ad in db.GeneratedAds.Where(a => a.BookId == book.Id && a.UserId == uid))
        {
            ad.CoverImageUrl = book.CoverImageUrl;
            ad.BookTitle = book.Title;
        }
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

    public Book? FindBookByTrackingCode(string trackingCode)
    {
        using var db = Db();
        var b = db.Books.Include(x => x.Links).AsNoTracking()
            .FirstOrDefault(x => x.TrackingCode == trackingCode);
        return b is null ? null : ToModel(b);
    }

    public Book? RecordClick(string trackingCode, string? platformSource = null)
    {
        using var db = Db();
        var b = db.Books.Include(x => x.Links).FirstOrDefault(x => x.TrackingCode == trackingCode);
        if (b is null) return null;
        b.MonthlyClicks++;
        var key = DateTime.UtcNow.ToString("yyyy-MM");
        var hist = JsonSerializer.Deserialize<Dictionary<string, int>>(b.ClickHistoryJson) ?? [];
        hist[key] = hist.TryGetValue(key, out var prev) ? prev + 1 : 1;
        b.ClickHistoryJson = JsonSerializer.Serialize(hist);

        var platform = PlatformClickSource.Normalize(platformSource);
        var platformHist = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(b.PlatformClickHistoryJson) ?? [];
        if (!platformHist.TryGetValue(key, out var monthPlatforms))
        {
            monthPlatforms = [];
            platformHist[key] = monthPlatforms;
        }
        monthPlatforms[platform] = monthPlatforms.TryGetValue(platform, out var platformPrev) ? platformPrev + 1 : 1;
        b.PlatformClickHistoryJson = JsonSerializer.Serialize(platformHist);

        db.SaveChanges();
        return ToModel(b);
    }

    // ── Social Accounts ────────────────────────────────────────────────
    public List<SocialAccount> SocialAccounts => AuthorSocialAccounts;

    public List<SocialAccount> AuthorSocialAccounts
    {
        get
        {
            var uid = CurrentUserId();
            if (uid == 0) return [];
            using var db = Db();
            return db.SocialAccounts
                .Where(a => a.UserId == uid && (a.AccountKind == SocialAccountKinds.Author || a.AccountKind == ""))
                .AsNoTracking().ToList().Select(ToModel).ToList();
        }
    }

    public SocialAccount AddSocialAccount(SocialAccount account, string? accountKind = null)
    {
        var uid = CurrentUserId();
        var kind = accountKind ?? account.AccountKind;
        if (string.IsNullOrWhiteSpace(kind)) kind = SocialAccountKinds.Author;
        if (SocialAccountKinds.IsBrand(kind) && !IsOwner)
            throw new InvalidOperationException("Only the owner can add BookPromoter AI brand accounts.");

        using var db = Db();
        var dbAcc = new DbSocialAccount
        {
            UserId = uid,
            Platform = account.Platform,
            DisplayName = account.DisplayName,
            Handle = account.Handle,
            IsConnected = account.IsConnected,
            ConnectedViaOAuth = account.ConnectedViaOAuth,
            AccountKind = kind,
            AccessToken = account.AccessToken ?? account.SimulatedAccessToken,
            RefreshToken = account.RefreshToken,
            ExternalAccountId = account.ExternalAccountId
        };
        db.SocialAccounts.Add(dbAcc);
        db.SaveChanges();
        account.Id = dbAcc.Id;
        account.AccountKind = kind;
        return account;
    }

    public void UpdateSocialAccount(SocialAccount account, string? accountKind = null)
    {
        var uid = CurrentUserId();
        var kind = accountKind ?? account.AccountKind;
        using var db = Db();
        var existing = db.SocialAccounts.FirstOrDefault(a => a.Id == account.Id && a.UserId == uid);
        if (existing is null) return;
        if (SocialAccountKinds.IsBrand(existing.AccountKind) && !IsOwner) return;
        if (!string.IsNullOrWhiteSpace(kind)) existing.AccountKind = kind;
        existing.Platform = account.Platform;
        existing.DisplayName = account.DisplayName;
        existing.Handle = account.Handle;
        db.SaveChanges();
    }

    public SocialAccount? FindSocialAccount(int id, string? accountKind = null)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var query = db.SocialAccounts.Where(x => x.Id == id && x.UserId == uid);
        if (!string.IsNullOrWhiteSpace(accountKind))
            query = query.Where(x => x.AccountKind == accountKind);
        var a = query.FirstOrDefault();
        return a is null ? null : ToModel(a);
    }

    public void RemoveSocialAccount(int id, string? accountKind = null)
    {
        var uid = CurrentUserId();
        using var db = Db();
        var query = db.SocialAccounts.Where(a => a.Id == id && a.UserId == uid);
        if (!string.IsNullOrWhiteSpace(accountKind))
            query = query.Where(a => a.AccountKind == accountKind);
        var acc = query.FirstOrDefault();
        if (acc is null) return;
        if (SocialAccountKinds.IsBrand(acc.AccountKind) && !IsOwner) return;
        db.SocialAccounts.Remove(acc);
        db.SaveChanges();
    }

    public string? CheckSocialAccountLimit(string accountKind = SocialAccountKinds.Author)
    {
        if (SocialAccountKinds.IsBrand(accountKind)) return null;
        var plan = CurrentPlan;
        if (plan?.SocialAccountLimit is int l && AuthorSocialAccounts.Count >= l) return $"You've reached the {l}-account limit on the {plan.Name} plan.";
        return null;
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
        var (weekNum, weekYear, weekLabel) = AdWeek.For(now);
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
        RefreshGeneratedAdEntity(ad, book, generator, baseUrl);
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

        var trackingUrl = PostBranding.PurchaseUrlForPost(book, baseUrl);
        var (subject, body) = generator.Generate(book, trackingUrl, book.PostVariantSeed);
        return (subject, body, book.Id, null);
    }

    public List<GeneratedAd> GenerateWeeklyPosts(PostGenerator generator, string baseUrl)
    {
        var touched = new List<GeneratedAd>();
        var books = Books;
        var activeSchedules = Schedules.Where(s => s.PostsPerWeek > 0).ToList();
        if (books.Count == 0 || activeSchedules.Count == 0) return touched;

        var now = DateTime.UtcNow;
        var (currentWeek, currentYear, weekLabel) = AdWeek.For(now);
        var uid = CurrentUserId();

        using var db = Db();
        var booksById = db.Books.Include(b => b.Links).Where(b => b.UserId == uid).ToDictionary(b => b.Id);
        var weekAds = db.GeneratedAds
            .Where(a => a.UserId == uid && a.WeekNumber == currentWeek && a.WeekYear == currentYear)
            .ToList();

        foreach (var ad in weekAds)
        {
            if (ad.ApprovedForPosting || ad.PostStatus == "Posted") continue;
            if (!booksById.TryGetValue(ad.BookId, out var book)) continue;
            RefreshGeneratedAdEntity(ad, book, generator, baseUrl);
            ad.WeekLabel = weekLabel;
            touched.Add(ToModel(ad));
        }

        var postsThisWeekByPlatform = weekAds
            .GroupBy(a => a.Platform)
            .ToDictionary(g => g.Key, g => g.Count());

        var bookIndex = 0;
        foreach (var schedule in activeSchedules)
        {
            var existingCount = postsThisWeekByPlatform.GetValueOrDefault(schedule.Platform, 0);
            var needed = schedule.PostsPerWeek - existingCount;
            if (needed <= 0) continue;

            for (var i = 0; i < needed; i++)
            {
                var bookModel = books[bookIndex % books.Count];
                if (!booksById.TryGetValue(bookModel.Id, out var dbBook)) { bookIndex++; continue; }

                dbBook.PostVariantSeed++;
                var refreshedModel = ToModel(dbBook);
                var purchaseUrl = PostBranding.PurchaseUrlForPost(refreshedModel, baseUrl, schedule.Platform);
                var text = generator.Generate(refreshedModel, schedule.Platform, purchaseUrl, dbBook.PostVariantSeed, baseUrl);
                var created = new DbGeneratedAd
                {
                    UserId = uid,
                    BookId = dbBook.Id,
                    BookTitle = dbBook.Title,
                    CoverImageUrl = dbBook.CoverImageUrl,
                    Platform = schedule.Platform,
                    PostText = text,
                    GeneratedAt = now,
                    WeekNumber = currentWeek,
                    WeekYear = currentYear,
                    WeekLabel = weekLabel
                };
                db.GeneratedAds.Add(created);
                weekAds.Add(created);
                postsThisWeekByPlatform[schedule.Platform] = postsThisWeekByPlatform.GetValueOrDefault(schedule.Platform, 0) + 1;
                touched.Add(ToModel(created));
                bookIndex++;
            }
        }

        db.SaveChanges();
        return touched;
    }

    static void RefreshGeneratedAdEntity(DbGeneratedAd ad, DbBook book, PostGenerator generator, string baseUrl)
    {
        book.PostVariantSeed++;
        var model = ToModel(book);
        var purchaseUrl = PostBranding.PurchaseUrlForPost(model, baseUrl, ad.Platform);
        ad.PostText = generator.Generate(model, ad.Platform, purchaseUrl, book.PostVariantSeed, baseUrl);
        ad.CoverImageUrl = book.CoverImageUrl;
        ad.BookTitle = book.Title;
        ad.GeneratedAt = DateTime.UtcNow;
        ad.ApprovedForPosting = false;
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
        db.MailingListSubscribers.Add(new DbMailingListSubscriber
        {
            UserId = uid,
            Email = cleanEmail,
            Name = name.Trim(),
            SubscribedAt = DateTime.UtcNow,
            Source = source,
            UnsubscribeToken = NewUnsubscribeToken()
        });
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

    public (bool Success, string Message) UnsubscribeFromMailingList(int subscriptionId)
    {
        if (!IsLoggedIn || string.IsNullOrWhiteSpace(LoggedInEmail))
            return (false, "Log in to manage your email preferences.");

        var email = LoggedInEmail.Trim().ToLowerInvariant();
        using var db = Db();
        var sub = db.MailingListSubscribers.FirstOrDefault(s => s.Id == subscriptionId && s.Email == email);
        if (sub is null) return (false, "Subscription not found.");
        db.MailingListSubscribers.Remove(sub);
        db.SaveChanges();
        return (true, "You've been unsubscribed from that mailing list.");
    }

    public List<MailingListSubscription> GetMailingListSubscriptionsForLoggedInUser()
    {
        if (!IsLoggedIn || string.IsNullOrWhiteSpace(LoggedInEmail)) return [];
        var email = LoggedInEmail.Trim().ToLowerInvariant();
        using var db = Db();
        return db.MailingListSubscribers.AsNoTracking()
            .Where(s => s.Email == email)
            .Join(db.Users.AsNoTracking(), s => s.UserId, u => u.Id, (s, u) => new MailingListSubscription
            {
                Id = s.Id,
                ListOwnerEmail = u.Email,
                SubscribedAt = s.SubscribedAt,
                Source = s.Source
            })
            .OrderByDescending(s => s.SubscribedAt)
            .ToList();
    }

    public (bool Success, string Message, string? AuthorEmail) UnsubscribeByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, "Invalid unsubscribe link.", null);
        using var db = Db();
        var cleanToken = token.Trim();
        var sub = db.MailingListSubscribers.FirstOrDefault(s => s.UnsubscribeToken == cleanToken);
        if (sub is null) return (false, "This unsubscribe link is not valid or has already been used.", null);
        var author = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == sub.UserId);
        db.MailingListSubscribers.Remove(sub);
        db.SaveChanges();
        return (true, "You've been unsubscribed. You won't receive further emails from this list.", author?.Email);
    }

    public string? GetUnsubscribeUrl(string? appBaseUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(appBaseUrl) || string.IsNullOrWhiteSpace(token)) return null;
        return $"{appBaseUrl.TrimEnd('/')}/readers/unsubscribe/{Uri.EscapeDataString(token)}";
    }

    public void EnsureMailingListUnsubscribeTokens()
    {
        using var db = Db();
        var updated = false;
        foreach (var sub in db.MailingListSubscribers.Where(s => string.IsNullOrEmpty(s.UnsubscribeToken)))
        {
            sub.UnsubscribeToken = NewUnsubscribeToken();
            updated = true;
        }
        if (updated) db.SaveChanges();
    }

    static string NewUnsubscribeToken() => Guid.NewGuid().ToString("N");

    public (bool Success, string Message, string? AuthorLabel) SubscribeToMailingListByUserCode(string userCode, string email, string name)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanEmail) || !cleanEmail.Contains('@')) return (false, "Enter a valid email address.", null);
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.UserCode == userCode.Trim());
        if (user is null) return (false, "This signup link is not valid.", null);
        if (db.MailingListSubscribers.Any(s => s.UserId == user.Id && s.Email == cleanEmail)) return (false, "You're already subscribed to this mailing list.", user.Email);
        db.MailingListSubscribers.Add(new DbMailingListSubscriber
        {
            UserId = user.Id,
            Email = cleanEmail,
            Name = name.Trim(),
            SubscribedAt = DateTime.UtcNow,
            Source = "Signup",
            UnsubscribeToken = NewUnsubscribeToken()
        });
        db.SaveChanges();
        return (true, "You're subscribed! Watch your inbox for updates.", user.Email);
    }

    public string? GetAuthorEmailByUserCode(string userCode)
    {
        using var db = Db();
        return db.Users.AsNoTracking().FirstOrDefault(u => u.UserCode == userCode.Trim())?.Email;
    }

    public async Task<(int Sent, int Failed, string Message)> SendMailingListCampaignAsync(
        string subject, string body, string apiKey, string senderEmail, string senderName, string fromDisplayName, string? appBaseUrl = null)
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
            var unsubUrl = GetUnsubscribeUrl(appBaseUrl, sub.UnsubscribeToken);
            var ok = await EmailService.SendMailingListEmail(sub.Email, sub.Name, subject, body, fromDisplayName, apiKey, senderEmail, senderName, appBaseUrl, unsubUrl);
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
        // Access codes are created when a user signs up. Lifetime codes are owner-generated only.
    }

    public PromoCode? FindUnredeemedAccessCode(string email)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        using var db = Db();
        var promo = db.PromoCodes.AsNoTracking()
            .Where(p => !p.IsLifetimeFree && !p.IsRedeemed && p.IntendedRecipientEmail == cleanEmail)
            .OrderByDescending(p => p.Id)
            .FirstOrDefault();
        return promo is null ? null : ToModel(promo);
    }

    public PromoCode IssueAccessCodeForSignup(string email)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        using var db = Db();
        var existing = db.PromoCodes
            .Where(p => !p.IsLifetimeFree && !p.IsRedeemed && p.IntendedRecipientEmail == cleanEmail)
            .OrderByDescending(p => p.Id)
            .FirstOrDefault();
        if (existing is not null) return ToModel(existing);

        var code = GenerateCode("ACCESS-");
        var p = new DbPromoCode { Code = code, FreeTrialDays = 30, IntendedRecipientEmail = cleanEmail, IsLifetimeFree = false };
        db.PromoCodes.Add(p);
        db.SaveChanges();
        return ToModel(p);
    }

    public (List<PromoCode> Available, List<PromoCode> Redeemed, int RedeemedTotal) GetAccessCodesForDisplay()
    {
        using var db = Db();
        var available = db.PromoCodes.AsNoTracking()
            .Where(p => !p.IsLifetimeFree && !p.IsRedeemed)
            .OrderByDescending(p => p.Id)
            .ToList()
            .Select(ToModel)
            .ToList();

        var redeemed = FilterRedeemedAccessCodes(db);
        var redeemedTotal = redeemed.Count;
        var visible = redeemed
            .Take(PromoConstants.MaxVisiblePromoCodes)
            .ToList();
        return (available, visible, redeemedTotal);
    }

    public (List<PromoCode> Available, List<PromoCode> Redeemed, int RedeemedTotal) GetLifetimeCodesForDisplay()
    {
        using var db = Db();
        var available = db.PromoCodes.AsNoTracking()
            .Where(p => p.IsLifetimeFree && !p.IsRedeemed)
            .OrderByDescending(p => p.Id)
            .ToList()
            .Select(ToModel)
            .ToList();

        var redeemed = FilterRedeemedLifetimeCodes(db);
        var redeemedTotal = redeemed.Count;
        var visible = redeemed
            .Take(PromoConstants.MaxVisiblePromoCodes)
            .ToList();
        return (available, visible, redeemedTotal);
    }

    public (List<OwnerPlanMember> Visible, int TotalCount) GetPlanMembersForDisplay(string planId)
    {
        var ownerEmail = OwnerAccount.NormalizedEmail;
        using var db = Db();
        var paidUserIds = db.Subscriptions.AsNoTracking()
            .Where(s => s.PromoCodeUsed.StartsWith("Paid ("))
            .Select(s => s.UserId)
            .Distinct()
            .ToHashSet();

        var members = db.Users.AsNoTracking()
            .Where(u => u.CurrentPlanId == planId
                && u.HasCustomerAccess
                && u.Email != ownerEmail
                && u.AccessType != "Owner")
            .OrderByDescending(u => u.Id)
            .ToList()
            .Where(u => IsPaidPlanMember(u, paidUserIds))
            .ToList();

        var total = members.Count;
        var visible = members
            .Take(PromoConstants.MaxVisiblePromoCodes)
            .Select(u => new OwnerPlanMember
            {
                Email = u.Email,
                AccessType = u.AccessType,
                BillingLabel = DescribePlanMemberBilling(u),
                IsCancelled = u.IsCancelled,
                AccessEndsAt = u.SubscriptionEndsAt ?? u.AccessEndsAt
            })
            .ToList();
        return (visible, total);
    }

    static List<PromoCode> FilterRedeemedAccessCodes(AppDbContext db)
    {
        var promos = db.PromoCodes.AsNoTracking()
            .Where(p => !p.IsLifetimeFree && p.IsRedeemed)
            .OrderByDescending(p => p.RedeemedAt)
            .ThenByDescending(p => p.Id)
            .ToList();

        var results = new List<PromoCode>();
        foreach (var promo in promos)
        {
            var user = FindPromoRedeemer(db, promo);
            if (user is not null && IsActiveFreeTrialUser(user))
                results.Add(ToModel(promo));
        }
        return results;
    }

    static List<PromoCode> FilterRedeemedLifetimeCodes(AppDbContext db)
    {
        var promos = db.PromoCodes.AsNoTracking()
            .Where(p => p.IsLifetimeFree && p.IsRedeemed)
            .OrderByDescending(p => p.RedeemedAt)
            .ThenByDescending(p => p.Id)
            .ToList();

        var results = new List<PromoCode>();
        foreach (var promo in promos)
        {
            var user = FindPromoRedeemer(db, promo);
            if (user is not null && IsActiveLifetimeUser(user))
                results.Add(ToModel(promo));
        }
        return results;
    }

    static DbUser? FindPromoRedeemer(AppDbContext db, DbPromoCode promo)
    {
        var email = promo.RedeemedByEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            email = promo.IntendedRecipientEmail?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(email)
            ? null
            : db.Users.AsNoTracking().FirstOrDefault(u => u.Email == email);
    }

    static bool IsActiveFreeTrialUser(DbUser user) =>
        user.HasCustomerAccess && user.AccessType == "Free Trial";

    static bool IsActiveLifetimeUser(DbUser user) =>
        user.HasCustomerAccess && user.AccessType == "Lifetime Free (Publisher)";

    static bool IsPaidPlanMember(DbUser user, HashSet<int> paidUserIds)
    {
        if (user.AccessType is "Free Trial" or "Lifetime Free (Publisher)" or "No Access Selected")
            return false;

        if (!string.IsNullOrWhiteSpace(user.StripeSubscriptionId) || !string.IsNullOrWhiteSpace(user.PayPalSubscriptionId))
            return user.AccessType.EndsWith(" Subscription", StringComparison.Ordinal);

        return paidUserIds.Contains(user.Id) && user.AccessType.EndsWith(" Subscription", StringComparison.Ordinal);
    }

    static void FinalizeTrialGraduation(DbUser user, AppDbContext db)
    {
        var trialPromos = db.PromoCodes
            .Where(p => !p.IsLifetimeFree && p.IsRedeemed && p.RedeemedByEmail == user.Email)
            .ToList();
        db.PromoCodes.RemoveRange(trialPromos);

        var trialSubs = db.Subscriptions
            .Where(s => s.UserId == user.Id && !s.PromoCodeUsed.StartsWith("Paid ("))
            .ToList();
        db.Subscriptions.RemoveRange(trialSubs);
    }

    static string DescribePlanMemberBilling(DbUser u)
    {
        if (!string.IsNullOrWhiteSpace(u.StripeSubscriptionId))
            return string.IsNullOrWhiteSpace(u.BillingStatus) ? "Stripe subscription" : u.BillingStatus;
        if (!string.IsNullOrWhiteSpace(u.PayPalSubscriptionId))
            return "PayPal subscription";
        return u.AccessType;
    }

    public void SeedOwnerAccount()
    {
        using var db = Db();
        var email = OwnerAccount.NormalizedEmail;
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user is null)
        {
            user = new DbUser
            {
                Email = email,
                PasswordHash = PasswordHasher.Hash(OwnerAccount.Password),
                UserCode = GenerateCode("BPA-"),
                HasCustomerAccess = true,
                AccessType = "Owner",
                CurrentPlanId = "publisher",
                TermsAcceptedAt = DateTime.UtcNow,
                TermsAcceptedVersion = LegalConstants.CurrentTermsVersion
            };
            db.Users.Add(user);
        }
        else
        {
            user.PasswordHash = PasswordHasher.Hash(OwnerAccount.Password);
            user.HasCustomerAccess = true;
            if (string.IsNullOrWhiteSpace(user.AccessType) || user.AccessType == "No Access Selected")
                user.AccessType = "Owner";
            if (string.IsNullOrWhiteSpace(user.CurrentPlanId))
                user.CurrentPlanId = "publisher";
        }

        db.SaveChanges();
        if (LoggedInEmail == email) ClearUserCache();
        SyncAllUsersToOwnerMailingList();
        EnsureMailingListUnsubscribeTokens();
    }

    public PromoCode GenerateAccessCode(string? email = null)
    {
        // Legacy helper — access codes are issued on signup only.
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Access codes are created automatically when a user signs up.");
        return IssueAccessCodeForSignup(email);
    }

    public PromoCode GenerateLifetimeCode(string? email = null)
    {
        using var db = Db();
        var code = GenerateCode("LIFETIME-");
        var p = new DbPromoCode { Code = code, IsLifetimeFree = true, IntendedRecipientEmail = email };
        db.PromoCodes.Add(p); db.SaveChanges();
        return ToModel(p);
    }

    public (bool Success, string Message) DeletePromoCode(int promoId)
    {
        using var db = Db();
        var promo = db.PromoCodes.FirstOrDefault(p => p.Id == promoId);
        if (promo is null) return (false, "Promo code not found.");

        var codeLabel = promo.Code;
        var revokedEmails = new List<string>();

        foreach (var user in FindUsersAffectedByPromoDelete(promo, db))
        {
            if (RevokeAccessForPromo(user, promo, db))
                revokedEmails.Add(user.Email);
        }

        db.PromoCodes.Remove(promo);
        db.SaveChanges();
        ClearUserCache();

        if (revokedEmails.Count > 0)
            return (true, $"Deleted {codeLabel} and removed access for {string.Join(", ", revokedEmails)}.");
        if (promo.IsRedeemed)
            return (true, $"Deleted {codeLabel}. The user kept access (paid subscription or other active access).");
        return (true, $"Deleted unused code {codeLabel}.");
    }

    static IEnumerable<DbUser> FindUsersAffectedByPromoDelete(DbPromoCode promo, AppDbContext db)
    {
        var users = new List<DbUser>();
        var seenIds = new HashSet<int>();

        void TryAdd(DbUser? user)
        {
            if (user is null || seenIds.Contains(user.Id) || OwnerAccount.IsOwnerEmail(user.Email))
                return;
            seenIds.Add(user.Id);
            users.Add(user);
        }

        var redeemedEmail = promo.RedeemedByEmail?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(redeemedEmail))
            TryAdd(db.Users.FirstOrDefault(u => u.Email == redeemedEmail));

        var intendedEmail = promo.IntendedRecipientEmail?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(intendedEmail))
            TryAdd(db.Users.FirstOrDefault(u => u.Email == intendedEmail));

        var linkedUserIds = db.Subscriptions
            .Where(s => s.PromoCodeUsed == promo.Code)
            .Select(s => s.UserId)
            .Distinct();
        foreach (var userId in linkedUserIds)
            TryAdd(db.Users.FirstOrDefault(u => u.Id == userId));

        return users;
    }

    static bool RevokeAccessForPromo(DbUser user, DbPromoCode promo, AppDbContext db)
    {
        if (!string.IsNullOrWhiteSpace(user.StripeSubscriptionId) || !string.IsNullOrWhiteSpace(user.PayPalSubscriptionId))
            return false;

        var userSubs = db.Subscriptions.Where(s => s.UserId == user.Id).ToList();
        if (userSubs.Any(s => s.PromoCodeUsed.StartsWith("Paid (", StringComparison.Ordinal)))
            return false;

        var redeemedEmail = promo.RedeemedByEmail?.Trim().ToLowerInvariant();
        var linkedToPromo = userSubs.Any(s => s.PromoCodeUsed == promo.Code);
        var redeemedByUser = promo.IsRedeemed && redeemedEmail == user.Email;
        var accessFromPromo = linkedToPromo
            || redeemedByUser
            || (promo.IsLifetimeFree && user.AccessType == "Lifetime Free (Publisher)")
            || (!promo.IsLifetimeFree && user.AccessType == "Free Trial");

        if (!accessFromPromo)
            return false;

        foreach (var sub in userSubs.Where(s => s.PromoCodeUsed == promo.Code))
            db.Subscriptions.Remove(sub);

        if (userSubs.Any(s => s.PromoCodeUsed != promo.Code))
            return false;

        user.HasCustomerAccess = false;
        user.AccessType = "No Access Selected";
        user.CurrentPlanId = null;
        user.AccessEndsAt = null;
        user.IsCancelled = false;
        user.SubscriptionEndsAt = null;
        return true;
    }

    public void SubscribeEmailToOwnerMailingList(string email, string name = "", string source = "Signup")
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanEmail) || OwnerAccount.IsOwnerEmail(cleanEmail)) return;

        using var db = Db();
        var owner = db.Users.FirstOrDefault(u => u.Email == OwnerAccount.NormalizedEmail);
        if (owner is null) return;
        if (db.MailingListSubscribers.Any(s => s.UserId == owner.Id && s.Email == cleanEmail)) return;

        db.MailingListSubscribers.Add(new DbMailingListSubscriber
        {
            UserId = owner.Id,
            Email = cleanEmail,
            Name = name.Trim(),
            SubscribedAt = DateTime.UtcNow,
            Source = source,
            UnsubscribeToken = NewUnsubscribeToken()
        });
        db.SaveChanges();
    }

    public int SyncAllUsersToOwnerMailingList()
    {
        using var db = Db();
        var owner = db.Users.FirstOrDefault(u => u.Email == OwnerAccount.NormalizedEmail);
        if (owner is null) return 0;

        var existing = db.MailingListSubscribers
            .Where(s => s.UserId == owner.Id)
            .Select(s => s.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var user in db.Users.Where(u => u.Email != OwnerAccount.NormalizedEmail))
        {
            if (existing.Contains(user.Email)) continue;
            db.MailingListSubscribers.Add(new DbMailingListSubscriber
            {
                UserId = owner.Id,
                Email = user.Email,
                Name = "",
                SubscribedAt = DateTime.UtcNow,
                Source = "Auto sync",
                UnsubscribeToken = NewUnsubscribeToken()
            });
            added++;
        }

        if (added > 0) db.SaveChanges();
        return added;
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

    public bool AccountExists(string email)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        using var db = Db();
        return db.Users.Any(u => u.Email == cleanEmail);
    }

    public PromoRedeemResult Register(string email, string password, bool acceptedTerms)
    {
        var cleanEmail = email.Trim().ToLowerInvariant();
        if (OwnerAccount.IsOwnerEmail(cleanEmail))
            return new(false, "This email is reserved for the site owner.");
        if (string.IsNullOrWhiteSpace(cleanEmail) || string.IsNullOrWhiteSpace(password)) return new(false, "Enter an email and password.");
        if (!acceptedTerms) return new(false, "You must accept the Terms & Conditions to create an account.");
        using var db = Db();
        if (db.Users.Any(u => u.Email == cleanEmail)) return new(false, "An account with that email already exists.");
        var code = GenerateCode("BPA-");
        var user = new DbUser
        {
            Email = cleanEmail,
            PasswordHash = PasswordHasher.Hash(password),
            UserCode = code,
            TermsAcceptedAt = DateTime.UtcNow,
            TermsAcceptedVersion = LegalConstants.CurrentTermsVersion
        };
        db.Users.Add(user); db.SaveChanges();
        IssueAccessCodeForSignup(cleanEmail);
        SubscribeEmailToOwnerMailingList(cleanEmail, source: "Signup");
        LoggedInEmail = cleanEmail;
        return new(true, "Account created. Check your email for your 30-day access code.");
    }

    public PromoRedeemResult AcceptTerms()
    {
        if (!IsLoggedIn) return new(false, "Please log in first.");
        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == LoggedInEmail);
        if (user is null) return new(false, "Account not found.");
        user.TermsAcceptedAt = DateTime.UtcNow;
        user.TermsAcceptedVersion = LegalConstants.CurrentTermsVersion;
        db.SaveChanges();
        ClearUserCache();
        return new(true, "Thank you. You may now use BookPromoter AI.");
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
        ClearUserCache();
    }

    public void DeleteAccount()
    {
        if (LoggedInEmail is null) return;
        if (OwnerAccount.IsOwnerEmail(LoggedInEmail)) return;
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
    public PromoRedeemResult RedeemPromoCode(string? email, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return new(false, "Enter a promotional code.");

        var cleanEmail = string.IsNullOrWhiteSpace(email)
            ? LoggedInEmail?.Trim().ToLowerInvariant()
            : email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanEmail)) return new(false, "Log in or enter your email address.");

        using var db = Db();
        var user = db.Users.FirstOrDefault(u => u.Email == cleanEmail);
        if (user is null) return new(false, "No account found for that email.");
        if (!string.IsNullOrWhiteSpace(LoggedInEmail) && !user.Email.Equals(LoggedInEmail, StringComparison.OrdinalIgnoreCase))
            return new(false, "That code cannot be applied to this account.");

        if (user.AccessType == "Lifetime Free (Publisher)")
            return new(false, "You already have lifetime free access.");

        if (!string.IsNullOrWhiteSpace(user.StripeSubscriptionId) || !string.IsNullOrWhiteSpace(user.PayPalSubscriptionId))
            return new(false, "Cancel your paid subscription first, then apply a lifetime promotional code.");

        var promo = db.PromoCodes.FirstOrDefault(p => p.Code == code.Trim().ToUpperInvariant());
        if (promo is null) return new(false, "That promotional code is not valid.");
        if (promo.IsRedeemed) return new(false, "That promotional code has already been used.");
        if (!string.IsNullOrWhiteSpace(promo.IntendedRecipientEmail) && !promo.IntendedRecipientEmail.Equals(cleanEmail, StringComparison.OrdinalIgnoreCase))
            return new(false, "That promotional code was not assigned to this email.");

        var hasSubscription = db.Subscriptions.Any(s => s.UserId == user.Id);
        if (hasSubscription && !promo.IsLifetimeFree)
            return new(false, "This account already has access. Lifetime promotional codes can upgrade a trial; 30-day codes are for new accounts only.");

        promo.IsRedeemed = true;
        promo.RedeemedByEmail = cleanEmail;
        promo.RedeemedAt = DateTime.UtcNow;

        if (hasSubscription && promo.IsLifetimeFree)
        {
            FinalizeTrialGraduation(user, db);
            db.Subscriptions.Add(new DbSubscription
            {
                UserId = user.Id,
                Email = cleanEmail,
                TrialStartedAt = DateTime.UtcNow,
                TrialEndsAt = DateTime.MaxValue,
                PromoCodeUsed = promo.Code
            });
        }
        else
        {
            db.Subscriptions.Add(new DbSubscription
            {
                UserId = user.Id,
                Email = cleanEmail,
                TrialStartedAt = DateTime.UtcNow,
                TrialEndsAt = promo.IsLifetimeFree ? DateTime.MaxValue : DateTime.UtcNow.AddDays(promo.FreeTrialDays),
                PromoCodeUsed = promo.Code
            });
        }

        if (promo.IsLifetimeFree)
        {
            user.HasCustomerAccess = true;
            user.AccessType = "Lifetime Free (Publisher)";
            user.CurrentPlanId = "publisher";
            user.AccessEndsAt = null;
            user.IsCancelled = false;
            user.SubscriptionEndsAt = null;
        }
        else
        {
            user.HasCustomerAccess = true;
            user.AccessType = "Free Trial";
            user.AccessEndsAt = DateTime.UtcNow.AddDays(promo.FreeTrialDays);
            user.CurrentPlanId = "professional";
        }

        db.SaveChanges();
        ClearUserCache();
        return new(true, promo.IsLifetimeFree
            ? "Lifetime free Publisher access activated!"
            : $"Your {promo.FreeTrialDays}-day access code is active.");
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
        FinalizeTrialGraduation(user, db);
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

        FinalizeTrialGraduation(user, db);

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
        ClearUserCache();
        _ = GetCurrentUser();
    }

    private static bool SyncCustomerAccessState(DbUser user, AppDbContext db) =>
        RevokeExpiredAccessIfNeeded(user, db) || RevokeOrphanedPromoAccessIfNeeded(user, db);

    private static bool RevokeOrphanedPromoAccessIfNeeded(DbUser user, AppDbContext db)
    {
        if (!user.HasCustomerAccess || OwnerAccount.IsOwnerEmail(user.Email))
            return false;

        if (!string.IsNullOrWhiteSpace(user.StripeSubscriptionId) || !string.IsNullOrWhiteSpace(user.PayPalSubscriptionId))
            return false;

        var subs = db.Subscriptions.Where(s => s.UserId == user.Id).ToList();
        if (subs.Any(s => s.PromoCodeUsed.StartsWith("Paid (", StringComparison.Ordinal)))
            return false;

        var promoSubs = subs.Where(s => !s.PromoCodeUsed.StartsWith("Paid (", StringComparison.Ordinal)).ToList();
        if (promoSubs.Any(s => !db.PromoCodes.Any(p => p.Code == s.PromoCodeUsed)))
            return ClearPromoBasedAccess(user, db, promoSubs);

        if (user.AccessType is "Free Trial" or "Lifetime Free (Publisher)")
        {
            var isLifetime = user.AccessType == "Lifetime Free (Publisher)";
            var hasPromo = db.PromoCodes.Any(p =>
                p.IsRedeemed
                && p.RedeemedByEmail == user.Email
                && p.IsLifetimeFree == isLifetime);
            if (!hasPromo)
                return ClearPromoBasedAccess(user, db, promoSubs);
        }

        if (promoSubs.Count == 0
            && user.AccessType.EndsWith(" Subscription", StringComparison.Ordinal)
            && !db.PromoCodes.Any(p => p.IsRedeemed && p.RedeemedByEmail == user.Email))
            return ClearPromoBasedAccess(user, db, promoSubs);

        return false;
    }

    static bool ClearPromoBasedAccess(DbUser user, AppDbContext db, List<DbSubscription> promoSubs)
    {
        foreach (var sub in promoSubs)
            db.Subscriptions.Remove(sub);

        user.HasCustomerAccess = false;
        user.AccessType = "No Access Selected";
        user.CurrentPlanId = null;
        user.AccessEndsAt = null;
        user.IsCancelled = false;
        user.SubscriptionEndsAt = null;
        db.SaveChanges();
        return true;
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
        if (user is null || OwnerAccount.IsOwnerEmail(user.Email)) return null;
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
        if (OwnerAccount.IsOwnerEmail(user.Email)) return new(false, "This account password cannot be changed here.");
        user.PasswordHash = PasswordHasher.Hash(newPassword); user.ResetToken = null; user.ResetTokenExpiresAt = null;
        db.SaveChanges();
        return new(true, "Password updated. You can now log in.");
    }

    // ── Owner ──────────────────────────────────────────────────────────
    public void UpdatePlanPrice(string planId, decimal fee)
    {
        using var db = Db();
        var plan = db.SubscriptionPlans.Find(planId);
        if (plan is not null) { plan.MonthlyFee = Math.Max(0, fee); db.SaveChanges(); }
    }

    public void UpdatePlanPaymentIds(string planId, string? stripePriceId)
    {
        using var db = Db();
        var plan = db.SubscriptionPlans.Find(planId);
        if (plan is null) return;
        plan.StripePriceId = stripePriceId?.Trim() ?? "";
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

    public List<string> GetAutoPostBlockers(string platform)
    {
        var schedule = Schedules.FirstOrDefault(s => s.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));
        if (schedule is null || !schedule.AutoPostEnabled) return [];

        var blockers = new List<string>();
        if (schedule.PostsPerWeek <= 0)
            blockers.Add("Set posts/week above 0.");
        if (Books.Count == 0)
            blockers.Add("Add at least one book.");

        var pending = GeneratedAds
            .Where(a => a.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) && a.PostStatus == "Pending")
            .ToList();
        if (pending.Count == 0)
            blockers.Add("No pending posts yet — posts are created when you save the schedule or use Generate This Week's Posts in the Ad Library.");
        else if (schedule.RequiresApproval && pending.All(a => !a.ApprovedForPosting))
            blockers.Add("Approve posts in the Ad Library first (or turn off Approval required).");

        return blockers;
    }

    // ── Auto-posting (background scheduler) ───────────────────────────
    public async Task<int> RunDuePostsAsync(SocialPostingService postingService, int? userId = null)
    {
        var count = 0;
        var now = DateTime.UtcNow;
        var currentWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
        using var db = Db();

        var schedulesQuery = db.SocialSchedules.Where(s => s.AutoPostEnabled && s.PostsPerWeek > 0);
        if (userId is int uid)
            schedulesQuery = schedulesQuery.Where(s => s.UserId == uid);
        var schedules = await schedulesQuery.ToListAsync();
        foreach (var schedule in schedules)
        {
            if (schedule.WeekTrackerStart != currentWeek) { schedule.WeekTrackerStart = currentWeek; schedule.PostsSentThisWeek = 0; }
            if (schedule.PostsSentThisWeek >= schedule.PostsPerWeek) continue;
            var hoursBetween = (24.0 * 7) / schedule.PostsPerWeek;
            if (schedule.LastPostedAt is DateTime last && (now - last).TotalHours < hoursBetween) continue;
            var account = await db.SocialAccounts.FirstOrDefaultAsync(a =>
                a.UserId == schedule.UserId
                && a.Platform == schedule.Platform
                && (a.AccountKind == SocialAccountKinds.Author || a.AccountKind == ""));
            if (account is null) continue;
            var candidate = await db.GeneratedAds.Where(a => a.UserId == schedule.UserId && a.Platform == schedule.Platform && a.PostStatus == "Pending" && (!schedule.RequiresApproval || a.ApprovedForPosting)).OrderBy(a => a.GeneratedAt).FirstOrDefaultAsync();
            if (candidate is null) continue;
            var outcome = await postingService.PostAsync(ToModel(account), candidate.PostText);
            var result = outcome.Result;
            if (!string.IsNullOrWhiteSpace(outcome.AccessToken))
            {
                account.AccessToken = outcome.AccessToken;
                if (!string.IsNullOrWhiteSpace(outcome.RefreshToken))
                    account.RefreshToken = outcome.RefreshToken;
            }
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

    // ── Owner: app promotion & product updates ─────────────────────────
    public List<ProductUpdate> ProductUpdates
    {
        get
        {
            try
            {
                using var db = Db();
                return db.ProductUpdates.AsNoTracking()
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(20)
                    .Select(u => ToProductUpdate(u))
                    .ToList();
            }
            catch
            {
                return [];
            }
        }
    }

    public int RegisteredUserCount
    {
        get
        {
            using var db = Db();
            return db.Users.Count(u => u.Email.Contains("@"));
        }
    }

    public List<SocialAccount> OwnerSocialAccounts
    {
        get
        {
            using var db = Db();
            var owner = db.Users.AsNoTracking().FirstOrDefault(u => u.Email == OwnerAccount.NormalizedEmail);
            if (owner is null) return [];
            return db.SocialAccounts.AsNoTracking()
                .Where(a => a.UserId == owner.Id && a.IsConnected && a.AccountKind == SocialAccountKinds.Brand)
                .Select(ToModel)
                .ToList();
        }
    }

    public async Task<(int Sent, int Failed, string Message)> BroadcastAppEmailAsync(
        string subject, string body, string apiKey, string senderEmail, string senderName, string? appBaseUrl = null)
    {
        if (!IsOwner) return (0, 0, "Only the owner can send app-wide emails.");
        if (string.IsNullOrWhiteSpace(subject)) return (0, 0, "Enter an email subject.");
        if (string.IsNullOrWhiteSpace(body)) return (0, 0, "Enter a message to send.");

        var emails = GetAllUserEmails();
        if (emails.Count == 0) return (0, 0, "No registered users to email yet.");

        var (sent, failed) = await EmailService.SendBroadcastEmailAsync(emails, subject, body, apiKey, senderEmail, senderName, appBaseUrl);
        var devNote = !_settings.IsSendGridConfigured && sent > 0
            ? " (SendGrid not configured — logged only in dev.)"
            : "";
        var message = failed == 0
            ? $"Promo email sent to {sent} user(s).{devNote}"
            : $"Sent to {sent} user(s). {failed} failed.{devNote}";
        return (sent, failed, message);
    }

    public async Task<(int Posted, int Failed, string Message)> PostOwnerAppPromoAsync(
        SocialPostingService postingService, string appBaseUrl, string? platformFilter = null)
    {
        if (!IsOwner) return (0, 0, "Only the owner can post app promotions.");

        using var db = Db();
        var owner = db.Users.FirstOrDefault(u => u.Email == OwnerAccount.NormalizedEmail);
        if (owner is null) return (0, 0, "Owner account not found.");

        var accounts = await db.SocialAccounts
            .Where(a => a.UserId == owner.Id && a.IsConnected && a.AccountKind == SocialAccountKinds.Brand)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(platformFilter))
            accounts = accounts.Where(a => a.Platform.Equals(platformFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        if (accounts.Count == 0)
            return (0, 0, "Connect BookPromoter AI brand accounts under Owner → Owner Social Media Accounts, then try again.");

        var promoPosts = AppPromoGenerator.GeneratePromoPosts(appBaseUrl);
        var posted = 0;
        var failed = 0;
        var now = DateTime.UtcNow;

        foreach (var account in accounts)
        {
            var postText = promoPosts.GetValueOrDefault(account.Platform)
                ?? promoPosts.Values.FirstOrDefault()
                ?? "";
            if (string.IsNullOrWhiteSpace(postText)) continue;

            var outcome = await postingService.PostAsync(ToModel(account), postText);
            var result = outcome.Result;
            db.PostingLog.Add(new DbPostingLogEntry
            {
                UserId = owner.Id,
                Platform = account.Platform,
                BookTitle = "BookPromoter AI",
                Success = result.Success,
                Message = result.Message,
                AttemptedAt = now
            });
            if (result.Success) posted++; else failed++;
        }

        await db.SaveChangesAsync();
        var message = failed == 0
            ? $"Posted to {posted} connected account(s). Bluesky posts live when connected with an app password."
            : $"Posted to {posted} account(s). {failed} failed.";
        return (posted, failed, message);
    }

    public async Task<(bool Success, string Message, ProductUpdate? Update)> PublishProductUpdateAsync(
        string version,
        string title,
        string updatedItems,
        string createdItems,
        string addedItems,
        bool sendEmail,
        bool postToSocial,
        string appBaseUrl,
        SocialPostingService postingService,
        string apiKey,
        string senderEmail,
        string senderName)
    {
        if (!IsOwner) return (false, "Only the owner can publish product updates.", null);

        version = version.Trim();
        if (string.IsNullOrWhiteSpace(version)) return (false, "Enter a version number (e.g. 1.5.0).", null);

        var hasChanges = AppPromoGenerator.ParseLines(updatedItems).Count > 0
            || AppPromoGenerator.ParseLines(createdItems).Count > 0
            || AppPromoGenerator.ParseLines(addedItems).Count > 0;
        if (!hasChanges) return (false, "Add at least one item under Updated, New, or Added.", null);

        using var db = Db();
        var update = new DbProductUpdate
        {
            Version = version,
            Title = title.Trim(),
            UpdatedItems = updatedItems.Trim(),
            CreatedItems = createdItems.Trim(),
            AddedItems = addedItems.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var socialPosts = AppPromoGenerator.GenerateUpdatePosts(ToProductUpdate(update), appBaseUrl);
        update.SocialPostText = socialPosts.GetValueOrDefault("Facebook") ?? socialPosts.Values.FirstOrDefault();

        if (sendEmail)
        {
            var recipients = GetOwnerMailingListRecipients();
            if (recipients.Count == 0)
                return (false, "No mailing list subscribers to email. Users can re-subscribe from the public signup link.", null);

            var (sent, failed) = await EmailService.SendProductUpdateEmailAsync(
                recipients, ToProductUpdate(update), appBaseUrl, apiKey, senderEmail, senderName);
            update.EmailedAt = DateTime.UtcNow;
            update.EmailsSent = sent;
            update.EmailsFailed = failed;
        }

        if (postToSocial)
        {
            var owner = db.Users.FirstOrDefault(u => u.Email == OwnerAccount.NormalizedEmail);
            if (owner is not null)
            {
                var accounts = await db.SocialAccounts.Where(a => a.UserId == owner.Id && a.IsConnected && a.AccountKind == SocialAccountKinds.Brand).ToListAsync();
                foreach (var account in accounts)
                {
                    var text = socialPosts.GetValueOrDefault(account.Platform)
                        ?? AppPromoGenerator.GenerateUpdatePost(account.Platform, ToProductUpdate(update), appBaseUrl);
                    var outcome = await postingService.PostAsync(ToModel(account), text);
                    var result = outcome.Result;
                    if (!string.IsNullOrWhiteSpace(outcome.AccessToken))
                    {
                        account.AccessToken = outcome.AccessToken;
                        if (!string.IsNullOrWhiteSpace(outcome.RefreshToken))
                            account.RefreshToken = outcome.RefreshToken;
                    }
                    db.PostingLog.Add(new DbPostingLogEntry
                    {
                        UserId = owner.Id,
                        Platform = account.Platform,
                        BookTitle = $"Update v{version}",
                        Success = result.Success,
                        Message = result.Message,
                        AttemptedAt = DateTime.UtcNow
                    });
                    if (result.Success) update.SocialPostsSent++;
                }
            }
        }

        db.ProductUpdates.Add(update);
        await db.SaveChangesAsync();

        var parts = new List<string> { $"Saved update v{version}." };
        if (sendEmail) parts.Add($"Emailed {update.EmailsSent} user(s)" + (update.EmailsFailed > 0 ? $" ({update.EmailsFailed} failed)" : "") + ".");
        if (postToSocial) parts.Add($"Posted to {update.SocialPostsSent} social account(s).");
        if (sendEmail && !_settings.IsSendGridConfigured)
            parts.Add("SendGrid is not configured — emails were not actually delivered.");

        return (true, string.Join(" ", parts), ToProductUpdate(update));
    }

    List<string> GetAllUserEmails()
    {
        using var db = Db();
        return db.Users.AsNoTracking()
            .Where(u => u.Email.Contains("@"))
            .Select(u => u.Email.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    List<(string Email, string UnsubscribeToken)> GetOwnerMailingListRecipients()
    {
        using var db = Db();
        var owner = db.Users.AsNoTracking().FirstOrDefault(u => u.Email == OwnerAccount.NormalizedEmail);
        if (owner is null) return [];
        return db.MailingListSubscribers.AsNoTracking()
            .Where(s => s.UserId == owner.Id)
            .Select(s => new ValueTuple<string, string>(s.Email, s.UnsubscribeToken))
            .ToList();
    }

    static ProductUpdate ToProductUpdate(DbProductUpdate u) => new()
    {
        Id = u.Id,
        Version = u.Version,
        Title = u.Title,
        UpdatedItems = u.UpdatedItems,
        CreatedItems = u.CreatedItems,
        AddedItems = u.AddedItems,
        SocialPostText = u.SocialPostText,
        CreatedAt = u.CreatedAt,
        EmailedAt = u.EmailedAt,
        EmailsSent = u.EmailsSent,
        EmailsFailed = u.EmailsFailed,
        SocialPostsSent = u.SocialPostsSent
    };

    // ── Helpers ────────────────────────────────────────────────────────
    private static string GenerateCode(string prefix)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var suffix = new char[6];
        for (var i = 0; i < 6; i++) suffix[i] = chars[Random.Shared.Next(chars.Length)];
        return prefix + new string(suffix);
    }

    // ── Model converters ───────────────────────────────────────────────
    static Book ToModel(DbBook b) => new() { Id = b.Id, Title = b.Title, AuthorName = b.AuthorName, Genre = b.Genre, Description = b.Description, CoverImageUrl = b.CoverImageUrl, CoverSourceUrl = b.CoverSourceUrl, TrackingCode = b.TrackingCode, MonthlyClicks = b.MonthlyClicks, PostVariantSeed = b.PostVariantSeed, ClientId = b.ClientId, Links = b.Links.Select(l => new BookLink { StoreName = l.StoreName, Url = l.Url }).ToList(), ClickHistory = ParseClickHistory(b.ClickHistoryJson), PlatformClickHistory = ParsePlatformClickHistory(b.PlatformClickHistoryJson) };
    static DbBook ToDb(Book b, int uid) => new() { UserId = uid, Title = b.Title, AuthorName = b.AuthorName, Genre = b.Genre, Description = b.Description, CoverImageUrl = b.CoverImageUrl, CoverSourceUrl = b.CoverSourceUrl, TrackingCode = b.TrackingCode, MonthlyClicks = b.MonthlyClicks, PostVariantSeed = b.PostVariantSeed, ClientId = b.ClientId, ClickHistoryJson = JsonSerializer.Serialize(b.ClickHistory), PlatformClickHistoryJson = JsonSerializer.Serialize(b.PlatformClickHistory), Links = b.Links.Select(l => new DbBookLink { StoreName = l.StoreName, Url = l.Url }).ToList() };
    static SocialAccount ToModel(DbSocialAccount a) => new()
    {
        Id = a.Id,
        Platform = a.Platform,
        DisplayName = a.DisplayName,
        Handle = a.Handle,
        IsConnected = a.IsConnected,
        ConnectedViaOAuth = a.ConnectedViaOAuth,
        AccountKind = a.AccountKind,
        AccessToken = a.AccessToken,
        RefreshToken = a.RefreshToken,
        ExternalAccountId = a.ExternalAccountId,
        SimulatedAccessToken = a.AccessToken
    };
    static SocialSchedule ToModel(DbSocialSchedule s) => new() { Platform = s.Platform, PostsPerWeek = s.PostsPerWeek, RequiresApproval = s.RequiresApproval, AutoPostEnabled = s.AutoPostEnabled, LastPostedAt = s.LastPostedAt, PostsSentThisWeek = s.PostsSentThisWeek, WeekTrackerStart = s.WeekTrackerStart };
    static GeneratedAd ToModel(DbGeneratedAd a) => new() { Id = a.Id, BookId = a.BookId, BookTitle = a.BookTitle, CoverImageUrl = a.CoverImageUrl, Platform = a.Platform, PostText = a.PostText, GeneratedAt = a.GeneratedAt, WeekNumber = a.WeekNumber, WeekYear = a.WeekYear, WeekLabel = a.WeekLabel, PostStatus = a.PostStatus, PostedAt = a.PostedAt, PostError = a.PostError, ApprovedForPosting = a.ApprovedForPosting };
    static PostingLogEntry ToModel(DbPostingLogEntry l) => new() { Id = l.Id, GeneratedAdId = l.GeneratedAdId, Platform = l.Platform, BookTitle = l.BookTitle, Success = l.Success, Message = l.Message, AttemptedAt = l.AttemptedAt };

    static Dictionary<string, int> ParseClickHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? []; }
        catch { return []; }
    }

    static Dictionary<string, Dictionary<string, int>> ParsePlatformClickHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json) ?? []; }
        catch { return []; }
    }
    static TeamMember ToModel(DbTeamMember t) => new() { Email = t.Email, Role = t.Role, InviteCode = t.InviteCode, Accepted = t.Accepted, InvitedAt = t.InvitedAt };
    static Client ToModel(DbClient c) => new() { Id = c.Id, Name = c.Name, ContactEmail = c.ContactEmail, Notes = c.Notes };
    static PromoCode ToModel(DbPromoCode p) => new() { Id = p.Id, Code = p.Code, FreeTrialDays = p.FreeTrialDays, IntendedRecipientEmail = p.IntendedRecipientEmail, IsRedeemed = p.IsRedeemed, RedeemedByEmail = p.RedeemedByEmail, RedeemedAt = p.RedeemedAt, IsLifetimeFree = p.IsLifetimeFree };
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
    static MailingListSubscriber ToModel(DbMailingListSubscriber s) => new() { Id = s.Id, Email = s.Email, Name = s.Name, SubscribedAt = s.SubscribedAt, Source = s.Source, UnsubscribeToken = s.UnsubscribeToken };
    static MailingListCampaign ToModel(DbMailingListCampaign c) => new() { Id = c.Id, Subject = c.Subject, Body = c.Body, RecipientCount = c.RecipientCount, FailedCount = c.FailedCount, SentAt = c.SentAt };
}
