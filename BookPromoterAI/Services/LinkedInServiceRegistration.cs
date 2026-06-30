namespace BookPromoterAI;

static class LinkedInServiceRegistration
{
    public static IServiceCollection AddLinkedIn(this IServiceCollection services)
    {
        services.AddHttpClient<LinkedInService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
