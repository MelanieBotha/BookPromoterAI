namespace BookPromoterAI;

static class BlueskyServiceRegistration
{
    public static IServiceCollection AddBluesky(this IServiceCollection services)
    {
        services.AddHttpClient<BlueskyService>(client =>
        {
            client.BaseAddress = new Uri("https://bsky.social");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
