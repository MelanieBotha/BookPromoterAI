namespace BookPromoterAI;

static class WordPressServiceRegistration
{
    public static IServiceCollection AddWordPress(this IServiceCollection services)
    {
        services.AddHttpClient<WordPressService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        return services;
    }
}
