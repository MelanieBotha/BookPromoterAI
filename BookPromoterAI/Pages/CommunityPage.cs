using System.Text;

namespace BookPromoterAI;

static class CommunityPage
{
    public static string Render(CommunityProfile brand, bool isAuthorContext = false) => $"""
        <section class="hero">
            <div>
                <p class="eyebrow">Reader community</p>
                <h1>Stay close to new books and updates</h1>
                <p class="muted">{(isAuthorContext
                    ? "Add your Discord, Telegram, and reader-email links in My Account — they are included on posts for platforms that do not grow an audience by themselves."
                    : "BookPromoter AI posts to social platforms automatically, but Discord, Telegram, and email lists only grow when you share invite links. Join below or add yours in the app.")}</p>
            </div>
        </section>

        <section class="panel">
            <h2>Join us</h2>
            {RenderLinkCards(brand)}
        </section>

        <section class="panel">
            <h2>Platforms that need invite links</h2>
            <p class="muted small-text">These channels <strong>publish</strong> your promos but rarely bring new members on their own. Put invite links in bios, emails, and other social posts.</p>
            <ul class="landing-checklist">
                <li><strong>Discord</strong> — webhook posts to your server; share a permanent <code>discord.gg</code> invite elsewhere.</li>
                <li><strong>Telegram</strong> — bot posts to your channel; share <code>t.me/yourchannel</code> on X, Bluesky, email, etc.</li>
                <li><strong>Mailing list</strong> — each author has a signup link in Mailing List; promote it like any other channel.</li>
                <li><strong>WordPress / Medium / Flickr</strong> — readers must discover your blog; link it from social posts.</li>
                <li><strong>TikTok</strong> — connect on Videos so Follow links open your TikTok profile; send promos to your inbox or download them.</li>
                <li><strong>Mastodon</strong> — federated network; cross-link from X, Bluesky, and your site.</li>
            </ul>
            <p class="muted small-text">Facebook, X, Bluesky, Tumblr, and LinkedIn still benefit from community links, but their feeds help more people see each post.</p>
        </section>
        """;

    static string RenderLinkCards(CommunityProfile profile)
    {
        if (!profile.HasAny)
            return """<p class="muted">Community links are not configured yet. Authors: open <a href="/my-account">My Account</a> → Reader community links. Owner: set brand links on the <a href="/owner">Owner</a> page.</p>""";

        var cards = new StringBuilder();
        if (profile.HasDiscord)
            cards.Append(LinkCard("Discord", "Chat, ARC readers, launch updates", profile.DiscordUrl!));
        if (profile.HasTelegram)
            cards.Append(LinkCard("Telegram", "Quick new-release alerts", profile.TelegramUrl!));
        if (profile.HasMailingList)
            cards.Append(LinkCard("Reader emails", "New books in your inbox", profile.MailingListUrl!));
        if (profile.HasMastodon)
            cards.Append(LinkCard("Mastodon", "Open social feed", profile.MastodonUrl!));
        if (profile.HasBlog)
            cards.Append(LinkCard("Blog", "Longer updates and extras", profile.BlogUrl!));
        if (profile.HasTikTok)
            cards.Append(LinkCard("TikTok", "Short video promos", profile.TikTokUrl!));

        return $"""<div class="landing-feature-grid">{cards}</div>""";
    }

    static string LinkCard(string title, string text, string url) => $"""
        <article class="landing-feature-card panel">
            <h3>{H.Encode(title)}</h3>
            <p>{H.Encode(text)}</p>
            <p><a class="button secondary" href="{H.Encode(url)}" target="_blank" rel="noopener">Join</a></p>
        </article>
        """;
}
