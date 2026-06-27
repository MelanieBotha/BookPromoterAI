using Stripe;

using Stripe.Checkout;



namespace BookPromoterAI;



class StripeBillingService

{

    private readonly AppSettings _settings;

    private readonly IHostEnvironment _environment;



    public StripeBillingService(AppSettings settings, IHostEnvironment environment)

    {

        _settings = settings;

        _environment = environment;

        if (_settings.IsStripeConfigured)

            StripeConfiguration.ApiKey = _settings.StripeSecretKey;

    }



    string? ValidateStripeSecretKey(string key)

    {

        if (key.StartsWith("rk_", StringComparison.Ordinal))

            return "Use the standard Secret key (sk_live_... or sk_test_...) from Stripe → Developers → API keys, not a restricted key (rk_...).";

        if (!key.StartsWith("sk_", StringComparison.Ordinal))

            return "Stripe Secret Key should start with sk_live_ or sk_test_. Check Railway variable Stripe__SecretKey.";

        if (key.Any(char.IsWhiteSpace))

            return "Stripe Secret Key contains spaces or line breaks. Re-paste it in Railway with no extra characters.";

        if (_environment.IsProduction() && key.StartsWith("sk_test_", StringComparison.Ordinal))

            return "Production is using a Stripe test key (sk_test_...). Real customer payments require sk_live_... in Railway.";

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



        if (plan.MonthlyFee <= 0)

            return (false, null, "This plan has no monthly fee configured. Contact support.");



        StripeConfiguration.ApiKey = _settings.StripeSecretKey;



        var baseUrl = PublicUrl.Local(request);

        var expectedCents = (long)(plan.MonthlyFee * 100);

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

                UnitAmount = expectedCents,

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

                ["planId"] = planId,

                ["expectedAmountCents"] = expectedCents.ToString()

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

                ReturnUrl = $"{PublicUrl.Local(request)}/billing"

            });

            return (true, session.Url, null);

        }

        catch (StripeException ex)

        {

            return (false, null, ex.Message);

        }

    }



    public async Task<(bool Ok, string? PlanId, string? Error)> TryFulfillCheckoutSessionAsync(

        string sessionId, AppStoreDb store, int? expectedUserId = null)

    {

        if (!_settings.IsStripeConfigured || string.IsNullOrWhiteSpace(sessionId))

            return (false, null, "Missing checkout session.");



        StripeConfiguration.ApiKey = _settings.StripeSecretKey;



        Stripe.Checkout.Session session;

        try

        {

            session = await new SessionService().GetAsync(sessionId, new SessionGetOptions

            {

                Expand = ["subscription", "subscription.items.data.price"]

            });

        }

        catch (StripeException ex)

        {

            return (false, null, $"Could not verify payment with Stripe: {ex.Message}");

        }



        var planId = session.Metadata?.GetValueOrDefault("planId");

        var validationError = ValidateSessionForActivation(session, planId, store, expectedUserId);

        if (validationError is not null)

            return (false, planId, validationError);



        if (!ActivateFromSession(session, store))

            return (false, planId, "Could not activate your subscription. Contact support with your receipt.");



        return (true, planId, null);

    }



    public async Task<bool> FulfillCheckoutSessionAsync(string sessionId, AppStoreDb store)

    {

        var (ok, _, _) = await TryFulfillCheckoutSessionAsync(sessionId, store);

        return ok;

    }



    public async Task<bool> HandleWebhookAsync(string json, string signatureHeader, AppStoreDb store)

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

                if (stripeEvent.Data.Object is Stripe.Checkout.Session session && !string.IsNullOrWhiteSpace(session.Id))

                    await TryFulfillCheckoutSessionAsync(session.Id, store);

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



    string? ValidateSessionForActivation(

        Stripe.Checkout.Session session, string? planId, AppStoreDb store, int? expectedUserId)

    {

        if (session.Mode != "subscription")

            return "This checkout was not a subscription.";



        if (session.Status != "complete")

            return "Checkout was not completed. No charge was made.";



        var userId = ParseUserId(session.ClientReferenceId, session.Metadata);

        if (userId <= 0)

            return "Payment session is missing account information.";



        if (expectedUserId is int loggedInUserId && loggedInUserId != userId)

            return "This payment belongs to a different account. Sign in with the email used at checkout.";



        if (string.IsNullOrWhiteSpace(planId))

            return "Payment session is missing plan information.";



        var plan = store.GetDbPlan(planId);

        if (plan is null)

            return "The selected plan is no longer available.";



        if (plan.MonthlyFee > 0)

        {

            if (session.PaymentStatus != "paid")

                return $"Payment was not successful (status: {session.PaymentStatus}). Your card was not charged.";

        }

        else if (session.PaymentStatus is not "paid" and not "no_payment_required")

        {

            return $"Payment was not successful (status: {session.PaymentStatus}).";

        }



        if (string.IsNullOrWhiteSpace(session.SubscriptionId))

            return "No subscription was created. Your card was not charged.";



        var sub = session.Subscription as Stripe.Subscription

            ?? new SubscriptionService().Get(session.SubscriptionId, new SubscriptionGetOptions

            {

                Expand = ["items.data.price"]

            });



        if (sub.Status is not "active" and not "trialing")

            return $"Subscription is not active (status: {sub.Status}). Your card was not charged for this plan.";



        if (!SubscriptionMatchesPlan(sub, plan))

            return $"Payment amount does not match the {plan.Name} plan (${plan.MonthlyFee:0.00}/month). Contact support before retrying.";



        return null;

    }



    static bool SubscriptionMatchesPlan(Stripe.Subscription sub, DbSubscriptionPlan plan)

    {

        var item = sub.Items?.Data?.FirstOrDefault();

        var price = item?.Price;

        if (price is null) return false;



        if (!string.IsNullOrWhiteSpace(plan.StripePriceId))

            return price.Id == plan.StripePriceId;



        var expectedCents = (long)(plan.MonthlyFee * 100);

        return price.UnitAmount == expectedCents &&

               (price.Currency ?? "usd").Equals("usd", StringComparison.OrdinalIgnoreCase);

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

                var sub = session.Subscription as Stripe.Subscription

                    ?? new SubscriptionService().Get(subscriptionId);

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


