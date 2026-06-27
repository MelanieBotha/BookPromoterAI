using Stripe;
using Stripe.Checkout;

namespace BookPromoterAI;

class StripeBillingService
{
    private readonly AppSettings _settings;

    public StripeBillingService(AppSettings settings)
    {
        _settings = settings;
        if (_settings.IsStripeConfigured)
            StripeConfiguration.ApiKey = _settings.StripeSecretKey;
    }

    static string? ValidateStripeSecretKey(string key)
    {
        if (key.StartsWith("rk_", StringComparison.Ordinal))
            return "Use the standard Secret key (sk_live_... or sk_test_...) from Stripe → Developers → API keys, not a restricted key (rk_...).";
        if (!key.StartsWith("sk_", StringComparison.Ordinal))
            return "Stripe Secret Key should start with sk_live_ or sk_test_. Check Railway variable Stripe__SecretKey.";
        if (key.Any(char.IsWhiteSpace))
            return "Stripe Secret Key contains spaces or line breaks. Re-paste it in Railway with no extra characters.";
        return null;
    }

    public async Task<(bool Ok, string? Url, string? Error)> CreateCheckoutSessionAsync(
        HttpRequest request, DbUser user, DbSubscriptionPlan plan, string planId)
    {
        if (!_settings.IsStripeConfigured)
            return (false, null, "Stripe is not configured yet.");

        var keyError = ValidateStripeSecretKey(_settings.StripeSecretKey);
        if (keyError is not null)
            return (false, null, keyError);

        StripeConfiguration.ApiKey = _settings.StripeSecretKey;

        var baseUrl = PublicUrl.Base(request, _settings);
        var lineItem = new SessionLineItemOptions { Quantity = 1 };

        if (!string.IsNullOrWhiteSpace(plan.StripePriceId))
        {
            lineItem.Price = plan.StripePriceId;
        }
        else
        {
            lineItem.PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = "usd",
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = $"BookPromoter AI — {plan.Name}",
                    Description = $"Monthly subscription — {plan.Name} plan"
                },
                UnitAmount = (long)(plan.MonthlyFee * 100),
                Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = "month" }
            };
        }

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = $"{baseUrl}/subscription/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{baseUrl}/subscription?cancelled=1",
            ClientReferenceId = user.Id.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.Id.ToString(),
                ["planId"] = planId
            },
            LineItems = [lineItem]
        };

        if (!string.IsNullOrWhiteSpace(user.StripeCustomerId))
            options.Customer = user.StripeCustomerId;
        else
            options.CustomerEmail = user.Email;

        try
        {
            var session = await new SessionService().CreateAsync(options);
            return (true, session.Url, null);
        }
        catch (StripeException ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Ok, string? Url, string? Error)> CreatePortalSessionAsync(HttpRequest request, DbUser user)
    {
        if (!_settings.IsStripeConfigured)
            return (false, null, "Stripe is not configured yet.");
        if (string.IsNullOrWhiteSpace(user.StripeCustomerId))
            return (false, null, "No Stripe billing account on file yet.");

        try
        {
            var session = await new Stripe.BillingPortal.SessionService().CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = user.StripeCustomerId,
                ReturnUrl = $"{PublicUrl.Base(request, _settings)}/billing"
            });
            return (true, session.Url, null);
        }
        catch (StripeException ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<bool> FulfillCheckoutSessionAsync(string sessionId, AppStoreDb store)
    {
        if (!_settings.IsStripeConfigured || string.IsNullOrWhiteSpace(sessionId))
            return false;

        var session = await new SessionService().GetAsync(sessionId, new SessionGetOptions
        {
            Expand = ["subscription"]
        });

        if (session.Mode != "subscription" || session.Status != "complete")
            return false;

        return ActivateFromSession(session, store);
    }

    public bool HandleWebhook(string json, string signatureHeader, AppStoreDb store)
    {
        if (!_settings.IsStripeConfigured || string.IsNullOrWhiteSpace(_settings.StripeWebhookSecret))
            return false;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _settings.StripeWebhookSecret);
        }
        catch
        {
            return false;
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                if (stripeEvent.Data.Object is Stripe.Checkout.Session session)
                    ActivateFromSession(session, store);
                break;

            case "customer.subscription.updated":
                if (stripeEvent.Data.Object is Stripe.Subscription sub)
                    SyncSubscription(sub, store);
                break;

            case "customer.subscription.deleted":
                if (stripeEvent.Data.Object is Stripe.Subscription deleted)
                    store.MarkSubscriptionCancelledByProvider(deleted.Id, "stripe", GetSubscriptionPeriodEnd(deleted));
                break;

            case "invoice.payment_failed":
                if (stripeEvent.Data.Object is Stripe.Invoice invoice)
                {
                    var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
                    if (!string.IsNullOrWhiteSpace(subscriptionId))
                        store.SetBillingStatus(subscriptionId, "stripe", "past_due");
                }
                break;
        }

        return true;
    }

    public async Task<(bool Ok, string? Error)> CancelSubscriptionAsync(DbUser user)
    {
        if (string.IsNullOrWhiteSpace(user.StripeSubscriptionId))
            return (false, "No Stripe subscription to cancel.");

        try
        {
            await new SubscriptionService().UpdateAsync(user.StripeSubscriptionId, new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true
            });
            return (true, null);
        }
        catch (StripeException ex)
        {
            return (false, ex.Message);
        }
    }

    bool ActivateFromSession(Stripe.Checkout.Session session, AppStoreDb store)
    {
        var userId = ParseUserId(session.ClientReferenceId, session.Metadata);
        var planId = session.Metadata?.GetValueOrDefault("planId") ?? "";
        if (userId <= 0 || string.IsNullOrWhiteSpace(planId))
            return false;

        var subscriptionId = session.SubscriptionId;
        DateTime periodEnd = DateTime.UtcNow.AddMonths(1);
        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            try
            {
                var sub = new SubscriptionService().Get(subscriptionId);
                periodEnd = GetSubscriptionPeriodEnd(sub);
            }
            catch { /* use default */ }
        }

        return store.ActivatePaidSubscriptionFromProvider(
            userId,
            planId,
            "stripe",
            session.CustomerId,
            subscriptionId,
            null,
            periodEnd,
            "Stripe card");
    }

    void SyncSubscription(Stripe.Subscription sub, AppStoreDb store)
    {
        var periodEnd = GetSubscriptionPeriodEnd(sub);
        var status = sub.Status switch
        {
            "active" => "active",
            "past_due" => "past_due",
            "canceled" => "cancelled",
            _ => sub.Status ?? "active"
        };

        if (sub.CancelAtPeriodEnd && sub.Status == "active")
            store.MarkSubscriptionPendingCancel(sub.Id, "stripe", periodEnd);
        else if (sub.Status == "canceled")
            store.MarkSubscriptionCancelledByProvider(sub.Id, "stripe", periodEnd);
        else
            store.SyncProviderSubscription(sub.Id, "stripe", status, periodEnd);
    }

    static int ParseUserId(string? clientReferenceId, Dictionary<string, string>? metadata)
    {
        if (int.TryParse(clientReferenceId, out var id)) return id;
        if (metadata?.TryGetValue("userId", out var userId) == true && int.TryParse(userId, out id))
            return id;
        return 0;
    }

    static DateTime GetSubscriptionPeriodEnd(Stripe.Subscription sub)
    {
        var itemEnd = sub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        if (itemEnd.HasValue)
            return ToUtc(itemEnd.Value);
        if (sub.CancelAt.HasValue)
            return ToUtc(sub.CancelAt.Value);
        return DateTime.UtcNow.AddMonths(1);
    }

    static DateTime ToUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
}
