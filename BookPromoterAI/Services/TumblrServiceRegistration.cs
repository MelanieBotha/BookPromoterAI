namespace BookPromoterAI;

static class TumblrServiceRegistration
{
    public static IServiceCollection AddTumblr(this IServiceCollection services)
    {
        services.AddHttpClient<TumblrService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        return services;
    }
}
