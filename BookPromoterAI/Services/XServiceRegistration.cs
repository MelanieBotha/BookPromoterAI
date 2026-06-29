namespace BookPromoterAI;

static class XServiceRegistration
{
    public static IServiceCollection AddX(this IServiceCollection services)
    {
        services.AddHttpClient<XService>(client =>
        {
            client.BaseAddress = new Uri("https://api.twitter.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
