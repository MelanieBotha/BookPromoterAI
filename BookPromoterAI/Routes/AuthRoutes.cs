namespace BookPromoterAI;

static class AuthRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/start", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn)
                return Results.Content(H.RenderMarketingPage(http, "Start", AuthPages.StartLogin(""), store), "text/html");
            if (store.HasCustomerAccess)
                return Results.Redirect("/dashboard");
            return Results.Content(H.RenderPage(http, "Start", $"""
                <section class="hero">
                    <div><p class="eyebrow">Choose access</p><h1>Use an access code or pick a subscription plan.</h1></div>
                    <p class="muted">Signed in as <strong>{H.Encode(store.LoggedInEmail ?? "")}</strong></p>
                    <form method="post" action="/logout"><button class="button secondary" type="submit">Log Out</button></form>
                </section>
                <section class="panel">
                    <h2>Access Code</h2>
                    <p>Enter your email and we'll send you a 30-day access code.</p>
                    <a class="button" href="/trial">Get Access Code</a>
                </section>
                {BillingPage.PlansSection(store, "/subscription")}
                """, store), "text/html");
        });

        app.MapPost("/signup", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            var form = await request.ReadFormAsync();
            var result = store.Register(form["email"].ToString(), form["password"].ToString());
            if (!result.Success)
                return Results.Content(H.RenderMarketingPage(http, "Start", AuthPages.StartLogin($"""<div class="notice error">{H.Encode(result.Message)}</div>"""), store), "text/html");
            return Results.Redirect("/start");
        });

        app.MapPost("/login", async (HttpRequest request, AppStoreDb store) =>
        {
            var form = await request.ReadFormAsync();
            var result = store.Login(form["email"].ToString(), form["password"].ToString());
            if (!result.Success)
                return Results.Content(H.RenderMarketingPage(request.HttpContext, "Start", AuthPages.StartLogin($"""<div class="notice error">{H.Encode(result.Message)}</div>"""), store), "text/html");
            return Results.Redirect(store.HasCustomerAccess ? "/dashboard" : "/start");
        });

        app.MapGet("/logout", (AppStoreDb store) => { store.Logout(); return Results.Redirect("/"); });
        app.MapPost("/logout", (AppStoreDb store) => { store.Logout(); return Results.Redirect("/"); });

        app.MapGet("/forgot-password", (HttpContext http, AppStoreDb store) =>
            Results.Content(H.RenderMarketingPage(http, "Forgot Password", AuthPages.ForgotPassword(""), store), "text/html"));

        app.MapPost("/forgot-password", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            var form = await request.ReadFormAsync();
            var email = form["email"].ToString().Trim();
            var token = store.GeneratePasswordResetToken(email);
            if (token is not null)
            {
                var baseUrl = PublicUrl.Base(request, settings);
                var resetLink = $"{baseUrl}/reset-password?token={token}";
                await EmailService.SendPasswordResetEmail(email, resetLink, settings.SendGridApiKey, settings.SendGridSenderEmail, settings.SendGridSenderName);
                if (!settings.IsSendGridConfigured)
                {
                    var devNotice = $"""<div class="notice error"><strong>Dev mode:</strong> <a href="/reset-password?token={H.Encode(token)}">Click here to reset</a> (expires in 1 hour).</div>""";
                    return Results.Content(H.RenderMarketingPage(http, "Forgot Password", AuthPages.ForgotPassword(devNotice), store), "text/html");
                }
            }
            var notice = """<div class="notice success">If an account with that email exists, a reset link has been sent.</div>""";
            return Results.Content(H.RenderMarketingPage(http, "Forgot Password", AuthPages.ForgotPassword(notice), store), "text/html");
        });

        app.MapGet("/reset-password", (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            var token = request.Query["token"].ToString();
            if (string.IsNullOrWhiteSpace(token)) return Results.Redirect("/forgot-password");
            return Results.Content(H.RenderMarketingPage(http, "Reset Password", AuthPages.ResetPassword(token, ""), store), "text/html");
        });

        app.MapPost("/reset-password", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            var form = await request.ReadFormAsync();
            var result = store.ResetPassword(form["token"].ToString(), form["newPassword"].ToString());
            if (result.Success)
                return Results.Content(H.RenderMarketingPage(http, "Start", AuthPages.StartLogin($"""<div class="notice success">{H.Encode(result.Message)}</div>"""), store), "text/html");
            return Results.Content(H.RenderMarketingPage(http, "Reset Password", AuthPages.ResetPassword(form["token"].ToString(), $"""<div class="notice error">{H.Encode(result.Message)}</div>"""), store), "text/html");
        });

        app.MapGet("/trial", (HttpContext http, AppStoreDb store) =>
            Results.Content(H.RenderMarketingPage(http, "Access Code", AuthPages.TrialRequest(""), store), "text/html"));

        app.MapPost("/trial/request", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            var form = await request.ReadFormAsync();
            var email = form["email"].ToString().Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return Results.Content(H.RenderMarketingPage(http, "Access Code", AuthPages.TrialRequest("""<div class="notice error">Please enter your email address.</div>"""), store), "text/html");

            var promo = store.GenerateAccessCode(email);
            await EmailService.SendAccessCodeEmail(email, promo.Code, settings.SendGridApiKey, settings.SendGridSenderEmail, settings.SendGridSenderName);

            var devNotice = !settings.IsSendGridConfigured
                ? $"""<div class="notice error"><strong>Dev mode:</strong> Your access code is <strong>{H.Encode(promo.Code)}</strong> — enter it below.</div>"""
                : $"""<div class="notice success">Access code sent to <strong>{H.Encode(email)}</strong>. Check your inbox.</div>""";

            return Results.Content(H.RenderMarketingPage(http, "Access Code", AuthPages.TrialActivate(email, devNotice), store), "text/html");
        });

        app.MapGet("/trial/activate", (HttpContext http, AppStoreDb store) =>
            Results.Content(H.RenderMarketingPage(http, "Access Code", AuthPages.TrialActivate("", ""), store), "text/html"));

        app.MapPost("/trial/activate", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            var form = await request.ReadFormAsync();
            var result = store.RedeemPromoCode(form["email"].ToString(), form["promoCode"].ToString());
            if (result.Success) return Results.Redirect("/dashboard");
            var notice = $"""<div class="notice error">{H.Encode(result.Message)}</div>""";
            return Results.Content(H.RenderMarketingPage(http, "Access Code", AuthPages.TrialActivate(form["email"].ToString(), notice), store), "text/html");
        });

        app.MapPost("/trial", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            var form = await request.ReadFormAsync();
            var result = store.RedeemPromoCode(form["email"].ToString(), form["promoCode"].ToString());
            if (result.Success) return Results.Redirect("/dashboard");
            var notice = $"""<div class="notice error">{H.Encode(result.Message)}</div>""";
            return Results.Content(H.RenderMarketingPage(http, "Access Code", AuthPages.TrialActivate(form["email"].ToString(), notice), store), "text/html");
        });
    }
}
