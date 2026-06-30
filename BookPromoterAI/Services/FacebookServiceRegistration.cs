namespace BookPromoterAI;

static class FacebookServiceRegistration
{
    public static IServiceCollection AddFacebook(this IServiceCollection services)
    {
        services.AddHttpClient<FacebookService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
