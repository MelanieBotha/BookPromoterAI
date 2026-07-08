namespace BookPromoterAI;

static class FlickrServiceRegistration
{
    public static IServiceCollection AddFlickr(this IServiceCollection services)
    {
        services.AddHttpClient<FlickrService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(90);
        });
        return services;
    }
}
