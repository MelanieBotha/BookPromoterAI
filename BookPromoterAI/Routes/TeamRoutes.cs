namespace BookPromoterAI;

static class TeamRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/team", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (!store.CanSeeTeamAccess) return Results.Redirect("/dashboard");
            return Results.Content(H.RenderPage(http, "Team", TeamPage.Render(store, ""), store), "text/html");
        });

        app.MapPost("/team/invite", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess || !store.CanSeeTeamAccess) return Results.Redirect("/dashboard");
            if (!store.HasTeamAccess)
            {
                var locked = """<div class="notice error">Team invitations require the Publisher or Agency plan. <a href="/billing">Upgrade.</a></div>""";
                return Results.Content(H.RenderPage(http, "Team", TeamPage.Render(store, locked), store), "text/html");
            }
            var form = await request.ReadFormAsync();
            var (member, message) = store.AddTeamMember(form["email"].ToString(), form["role"].ToString());
            string notice;
            if (member is null)
            {
                notice = $"""<div class="notice error">{H.Encode(message)}</div>""";
            }
            else
            {
                await EmailService.SendTeamInviteEmail(form["email"].ToString(), member.InviteCode, member.Role, settings.SendGridApiKey, settings.SendGridSenderEmail, settings.SendGridSenderName);
                notice = !settings.IsSendGridConfigured
                    ? $"""<div class="notice success">{H.Encode(message)}<br><strong>Dev mode:</strong> Invite code: <strong>{H.Encode(member.InviteCode)}</strong></div>"""
                    : $"""<div class="notice success">{H.Encode(message)} Invite code emailed.</div>""";
            }
            return Results.Content(H.RenderPage(http, "Team", TeamPage.Render(store, notice), store), "text/html");
        });

        app.MapPost("/team/remove/{email}", (string email, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess || !store.HasTeamAccess) return Results.Redirect("/dashboard");
            store.RemoveTeamMember(Uri.UnescapeDataString(email));
            return Results.Redirect("/team");
        });
    }
}
