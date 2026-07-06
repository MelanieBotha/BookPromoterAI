namespace BookPromoterAI;

static class MastodonServiceRegistration
{
    public static IServiceCollection AddMastodonAndMessaging(this IServiceCollection services)
    {
        services.AddHttpClient<MastodonService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<DiscordTelegramPostingService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
