using Microsoft.AspNetCore.Antiforgery;

namespace BookPromoterAI;

static class SecurityMiddleware
{
    public static void UseBookPromoterSecurity(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method))
            {
                var path = context.Request.Path.Value ?? "";
                if (!path.StartsWith("/go/", StringComparison.OrdinalIgnoreCase))
                {
                    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                    try
                    {
                        await antiforgery.ValidateRequestAsync(context);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid or missing security token. Please refresh the page and try again.");
                        return;
                    }
                }
            }

            if (context.Request.Path.StartsWithSegments("/books") ||
                context.Request.Path.StartsWithSegments("/ad-library") ||
                context.Request.Path.StartsWithSegments("/analytics") ||
                context.Request.Path.StartsWithSegments("/billing") ||
                context.Request.Path.StartsWithSegments("/subscription") ||
                context.Request.Path.StartsWithSegments("/my-account") ||
                context.Request.Path.StartsWithSegments("/schedule") ||
                context.Request.Path.StartsWithSegments("/social-accounts") ||
                context.Request.Path.StartsWithSegments("/team") ||
                context.Request.Path.StartsWithSegments("/clients") ||
                context.Request.Path.StartsWithSegments("/mailing-list") ||
                context.Request.Path == "/dashboard")
            {
                var store = context.RequestServices.GetService<AppStoreDb>();
                store?.CheckAccessExpiry();
            }

            await next();
        });
    }
}
