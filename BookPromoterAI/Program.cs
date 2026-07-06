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
AppTimeZone.Configure(appSettings.DisplayTimeZoneId);
builder.Services.AddSingleton(appSettings);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // Railway (and similar proxies) terminate TLS; trust forwarded proto from any proxy.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
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
    options.UseSqlite($"Data Source={dbPath};Cache=Shared;Default Timeout=60"));

builder.Services.AddSingleton<ReleaseNotesCatalog>();
builder.Services.AddScoped<AppStoreDb>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<StripeBillingService>();

builder.Services.AddBluesky();
builder.Services.AddX();
builder.Services.AddLinkedIn();
builder.Services.AddFacebook();
builder.Services.AddReddit();
builder.Services.AddTikTok();
builder.Services.AddScoped<SocialPostingService>();

var generator = new PostGenerator();
var mailingListEmailGenerator = new MailingListEmailGenerator();
builder.Services.AddHostedService<DatabaseBootstrapHostedService>();
builder.Services.AddHostedService<PostingSchedulerServiceDb>();

var app = builder.Build();
var onRailway = !string.IsNullOrWhiteSpace(port);

// Liveness probe — always 200 so Railway deploy healthcheck passes while DB migrates.
app.MapGet("/health", () => Results.Text("ok"));
app.MapGet("/ready", () => DatabaseStartup.IsReady
    ? Results.Text("ok")
    : Results.Text(DatabaseStartup.LastError ?? "starting", statusCode: StatusCodes.Status503ServiceUnavailable));

app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.Equals("/ready", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/webhooks")
        || DatabaseStartup.IsReady)
    {
        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    await context.Response.WriteAsync("Starting up — please retry in a moment.");
});

app.UseForwardedHeaders();
if (!onRailway)
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
TikTokRoutes.Map(app, uploadsDir, generator);
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
