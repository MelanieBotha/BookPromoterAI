using BookPromoterAI;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT; local dev uses launchSettings.json (59874/59875).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var appSettings = AppSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(appSettings);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".BookPromoterAI.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
    if (!builder.Environment.IsDevelopment())
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    }
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

var dbPath = Environment.GetEnvironmentVariable("DATABASE_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "bookpromoter.db");

var legacyDbPath = Path.Combine(builder.Environment.ContentRootPath, "bookpromoter.db");
if (dbPath.Replace('\\', '/').Contains("/data/", StringComparison.OrdinalIgnoreCase)
    && !File.Exists(dbPath)
    && File.Exists(legacyDbPath))
{
    var dbDir = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dbDir))
        Directory.CreateDirectory(dbDir);
    File.Copy(legacyDbPath, dbPath);
}

var dbDirEnsure = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirEnsure))
    Directory.CreateDirectory(dbDirEnsure);

var uploadsPaths = UploadPaths.Resolve(dbPath, builder.Environment.ContentRootPath);
UploadPaths.EnsureReady(uploadsPaths.Path, builder.Environment.ContentRootPath);

builder.Services.AddSingleton(DatabasePaths.Resolve(dbPath));
builder.Services.AddSingleton(uploadsPaths);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton<ReleaseNotesCatalog>();
builder.Services.AddScoped<AppStoreDb>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<StripeBillingService>();

var generator = new PostGenerator();
var mailingListEmailGenerator = new MailingListEmailGenerator();
builder.Services.AddSingleton(new SocialPostingService());
builder.Services.AddHostedService<PostingSchedulerServiceDb>();

var app = builder.Build();

app.MapGet("/health", () => Results.Text("ok"));

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = dbFactory.CreateDbContext();
    DatabaseInitializer.ApplyMigrations(db);
    scope.ServiceProvider.GetRequiredService<AppStoreDb>().SeedPromoCodes();
    scope.ServiceProvider.GetRequiredService<AppStoreDb>().SeedOwnerAccount();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseSession();
app.UseBookPromoterSecurity();

var uploadsDir = uploadsPaths.Path;
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsDir),
    RequestPath = "/uploads"
});

LandingRoutes.Map(app);
LegalRoutes.Map(app);
DashboardRoutes.Map(app, generator);
BookRoutes.Map(app, generator, uploadsDir);
ScheduleRoutes.Map(app, generator);
SocialAccountRoutes.Map(app);
AdLibraryRoutes.Map(app, generator);
AuthRoutes.Map(app);
MyAccountRoutes.Map(app);
BillingRoutes.Map(app);
WebhookRoutes.Map(app);
TeamRoutes.Map(app);
AnalyticsRoutes.Map(app);
ClientRoutes.Map(app);
OwnerRoutes.Map(app);
FeedbackRoutes.Map(app);
HelpRoutes.Map(app);
MailingListRoutes.Map(app, mailingListEmailGenerator);

app.Run();
