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
                if (!path.StartsWith("/go/", StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith("/webhooks/", StringComparison.OrdinalIgnoreCase) &&
                    !path.StartsWith("/readers/", StringComparison.OrdinalIgnoreCase))
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

            var requestPath = context.Request.Path.Value ?? "";

            if (requestPath.Equals("/owner/promos", StringComparison.OrdinalIgnoreCase) ||
                requestPath.Equals("/owner_promos", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/owner-promos");
                return;
            }

            if (requestPath.StartsWith("/owner", StringComparison.OrdinalIgnoreCase))
            {
                var ownerStore = context.RequestServices.GetService<AppStoreDb>();
                if (ownerStore is not null && !ownerStore.IsOwner)
                {
                    context.Response.Redirect("/dashboard");
                    return;
                }
            }

            var store = context.RequestServices.GetService<AppStoreDb>();
            if (store?.IsLoggedIn == true && !store.HasAcceptedTerms && !IsTermsExemptPath(requestPath))
            {
                context.Response.Redirect("/accept-terms");
                return;
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
                store?.CheckAccessExpiry();
            }

            await next();
        });
    }

    static bool IsTermsExemptPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return true;
        if (path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/go/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/webhooks/", StringComparison.OrdinalIgnoreCase))
            return true;

        return path.Equals("/accept-terms", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/terms", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/terms-and-conditions", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/privacy", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/privacy-policy", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/logout", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/forgot-password", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/reset-password", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/readers/", StringComparison.OrdinalIgnoreCase);
    }
}
