namespace BookPromoterAI;

static class MediumServiceRegistration
{
    public static IServiceCollection AddMedium(this IServiceCollection services)
    {
        services.AddHttpClient<MediumService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        return services;
    }
}
