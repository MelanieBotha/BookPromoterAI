namespace BookPromoterAI;

static class InstagramServiceRegistration
{
    public static IServiceCollection AddInstagram(this IServiceCollection services)
    {
        services.AddHttpClient<InstagramService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        return services;
    }
}
