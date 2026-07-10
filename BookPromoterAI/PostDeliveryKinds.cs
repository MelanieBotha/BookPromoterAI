namespace BookPromoterAI;

static class PostDeliveryKinds
{
    public const string Scheduled = "Scheduled";
    public const string Manual = "Manual";

    public static string Label(string? delivery) =>
        delivery switch
        {
            Scheduled => "Scheduled",
            Manual => "Manual",
            _ => ""
        };
}
