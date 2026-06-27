using BookPromoterAI;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
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
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDir))
    Directory.CreateDirectory(dbDir);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<AppStoreDb>();

var generator = new PostGenerator();
var mailingListEmailGenerator = new MailingListEmailGenerator();
builder.Services.AddSingleton(new SocialPostingService());
builder.Services.AddHostedService<PostingSchedulerServiceDb>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = dbFactory.CreateDbContext();
    DatabaseInitializer.ApplyMigrations(db);
    scope.ServiceProvider.GetRequiredService<AppStoreDb>().SeedPromoCodes();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseSession();
app.UseBookPromoterSecurity();

var uploadsDir = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
Directory.CreateDirectory(uploadsDir);
app.UseStaticFiles();

LandingRoutes.Map(app);
DashboardRoutes.Map(app, generator);
BookRoutes.Map(app, generator, uploadsDir);
ScheduleRoutes.Map(app, generator);
SocialAccountRoutes.Map(app);
AdLibraryRoutes.Map(app, generator);
AuthRoutes.Map(app);
MyAccountRoutes.Map(app);
BillingRoutes.Map(app);
TeamRoutes.Map(app);
AnalyticsRoutes.Map(app);
ClientRoutes.Map(app);
OwnerRoutes.Map(app);
FeedbackRoutes.Map(app);
MailingListRoutes.Map(app, mailingListEmailGenerator);

app.Run();
