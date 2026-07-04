using System.Text;

namespace BookPromoterAI;

static class LandingPage
{
    public static string Render(AppStoreDb store) => $"""
        <section class="landing-hero">
            <div class="landing-hero-copy">
                <p class="eyebrow">For authors &amp; publishers</p>
                <h1>Promote your books on social media — without the daily grind.</h1>
                <p class="landing-lead">BookPromoter AI helps you manage your catalog, generate platform-ready posts, track clicks, and grow your readership from one place.</p>
                <div class="landing-cta-row">
                    <a class="button" href="/start">Create free account</a>
                    <a class="button secondary" href="/trial">Get 30-day access code</a>
                </div>
                <p class="muted small-text">No credit card required to start. Request a free access code or choose a plan after signup.</p>
            </div>
            <div class="landing-hero-card panel">
                <h2>Everything in one dashboard</h2>
                <ul class="landing-checklist">
                    <li>Book catalog with store links &amp; cover art</li>
                    <li>AI-generated posts for X, Reddit &amp; more</li>
                    <li>Weekly Ad Library with copy-to-clipboard</li>
                    <li>Click tracking on every promo link</li>
                    <li>Posting schedule &amp; mailing list tools</li>
                </ul>
            </div>
        </section>

        <section class="landing-section" id="features">
            <div class="landing-section-head">
                <p class="eyebrow">Features</p>
                <h2>Built for book promotion</h2>
                <p class="muted">From solo authors to agencies managing multiple clients.</p>
            </div>
            <div class="landing-feature-grid">
                {FeatureCard("📚", "Book catalog", "Add titles, genres, descriptions, and buy links. Upload covers or pull metadata automatically.")}
                {FeatureCard("✨", "AI post generation", "Genre-aware hooks and captions tailored for each social platform — ready to copy and share.")}
                {FeatureCard("📋", "Ad Library", "Generate a week's worth of posts at once. Search, regenerate, approve, and copy with one click.")}
                {FeatureCard("📅", "Smart scheduling", "Set how often you post on each platform. Review posts before they go live.")}
                {FeatureCard("📈", "Click analytics", "Track every promo link. See which books and posts drive the most reader interest.")}
                {FeatureCard("✉️", "Mailing list", "Collect reader emails, draft campaigns, and share a public signup link for each author.")}
            </div>
        </section>

        <section class="landing-section landing-steps panel">
            <div class="landing-section-head">
                <p class="eyebrow">How it works</p>
                <h2>Up and running in minutes</h2>
            </div>
            <ol class="landing-steps-list">
                <li><strong>Create your account</strong><span>Sign up free, then unlock access with a 30-day code or subscription plan.</span></li>
                <li><strong>Add your books</strong><span>Build your catalog with store links, covers, and genres.</span></li>
                <li><strong>Generate &amp; share posts</strong><span>Use the Ad Library to create posts, copy them to your platforms, and track clicks.</span></li>
            </ol>
        </section>

        {PlansPreview(store)}

        <section class="landing-section landing-final-cta panel">
            <h2>Ready to promote your books?</h2>
            <p class="muted">Join authors using BookPromoter AI to save time and reach more readers.</p>
            <div class="landing-cta-row">
                <a class="button" href="/start">Get started free</a>
                <a class="button secondary" href="/trial">Request access code</a>
            </div>
        </section>
        """;

    static string FeatureCard(string icon, string title, string text) => $"""
        <article class="landing-feature-card panel">
            <span class="landing-feature-icon" aria-hidden="true">{icon}</span>
            <h3>{H.Encode(title)}</h3>
            <p>{H.Encode(text)}</p>
        </article>
        """;

    static string PlansPreview(AppStoreDb store)
    {
        var cards = new StringBuilder();
        foreach (var plan in store.Plans)
        {
            var features = new StringBuilder();
            foreach (var feature in plan.Features.Take(4))
                features.Append($"<li>{H.Encode(feature)}</li>");

            cards.Append($"""
                <article class="panel plan-card landing-plan-card">
                    <h3>{H.Encode(plan.Name)}</h3>
                    <p class="price">${plan.MonthlyFee:0.00}<span> USD/month</span></p>
                    <ul class="plan-features">{features}</ul>
                    <a class="button" href="/start">Get started</a>
                </article>
                """);
        }

        return $"""
            <section class="landing-section" id="pricing">
                <div class="landing-section-head">
                    <p class="eyebrow">Pricing</p>
                    <h2>Plans for every stage</h2>
                    <p class="muted">Start with a free 30-day access code, or subscribe after creating your account. All prices in USD.</p>
                </div>
                <section class="choice-grid plans-grid landing-plans-grid">
                    {cards}
                </section>
                <p class="muted landing-pricing-note">Payment accepted worldwide by card or bank account. <a href="/subscription">View full plan details</a> after signing in.</p>
            </section>
            """;
    }
}
