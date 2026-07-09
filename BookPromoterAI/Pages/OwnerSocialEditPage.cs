using System.Text;

namespace BookPromoterAI;

static class OwnerSocialEditPage
{
    public static string Render(AppStoreDb store, SocialAccount account, string returnUrl)
    {
        var settings = store.Settings;
        var platformOptions = new StringBuilder();
        platformOptions.Append(SocialConnectHelper.RenderPlatformOption(account.Platform, selected: true, settings));
        foreach (var platform in SocialConnectHelper.DefaultPlatforms(settings, brandContext: true))
        {
            if (platform.Equals(account.Platform, StringComparison.OrdinalIgnoreCase)) continue;
            platformOptions.Append(SocialConnectHelper.RenderPlatformOption(platform, settings: settings));
        }
        platformOptions.Append("""<option value="__custom__">Other (type your own)...</option>""");

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">BookPromoter AI brand</p>
                    <h1>Edit brand social account</h1>
                </div>
            </section>
            <section class="panel form">
                <p class="muted">This account is used only for BookPromoter AI promotions — not author book posts.</p>
                <form method="post" action="/social-accounts/edit/{account.Id}">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Platform
                        <select name="platform">{platformOptions}</select>
                    </label>
                    <label>Display Name
                        <input name="displayName" value="{H.Encode(account.DisplayName)}" required>
                    </label>
                    <label>Handle
                        <input name="handle" value="{H.Encode(account.Handle)}" required>
                    </label>
                    <div class="form-actions">
                        <button class="button" type="submit">Save</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }
}
