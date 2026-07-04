namespace BookPromoterAI;

static class RedditServiceRegistration
{
    public static IServiceCollection AddReddit(this IServiceCollection services)
    {
        services.AddHttpClient<RedditService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
