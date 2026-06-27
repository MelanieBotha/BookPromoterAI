namespace BookPromoterAI;

static class ClientRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/clients", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (!store.CanSeeMultiClient) return Results.Redirect("/dashboard");
            return Results.Content(H.RenderPage(http, "Clients", ClientsPage.Render(store, ""), store), "text/html");
        });

        app.MapPost("/clients", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess || !store.CanSeeMultiClient) return Results.Redirect("/dashboard");
            if (!store.HasMultiClient)
            {
                var locked = """<div class="notice error">Multi-client management requires the Agency or Publisher plan. <a href="/billing">Upgrade.</a></div>""";
                return Results.Content(H.RenderPage(http, "Clients", ClientsPage.Render(store, locked), store), "text/html");
            }
            var form = await request.ReadFormAsync();
            store.AddClient(form["name"].ToString(), form["contactEmail"].ToString(), form["notes"].ToString());
            return Results.Redirect("/clients");
        });

        app.MapPost("/clients/delete/{id:int}", (int id, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess || !store.HasMultiClient) return Results.Redirect("/dashboard");
            store.RemoveClient(id);
            return Results.Redirect("/clients");
        });
    }
}
