namespace BookPromoterAI;

static class TumblrServiceRegistration
{
    public static IServiceCollection AddTumblr(this IServiceCollection services)
    {
        services.AddHttpClient<TumblrService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            // Tumblr requires a consistent User-Agent; varying values can get the consumer suspended.
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "BookPromoterAI/1.0 (+https://bookpromoterai.us)");
        });
        return services;
    }
}
