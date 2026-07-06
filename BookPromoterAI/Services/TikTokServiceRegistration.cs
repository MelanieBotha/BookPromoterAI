namespace BookPromoterAI;

static class TikTokServiceRegistration
{
    public static IServiceCollection AddTikTok(this IServiceCollection services)
    {
        services.AddHttpClient<TikTokService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        return services;
    }
}
