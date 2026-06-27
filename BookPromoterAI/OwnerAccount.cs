namespace BookPromoterAI;

/// <summary>Site owner credentials. Only this account sees Owner settings.</summary>
static class OwnerAccount
{
    public const string Email = "bothamelanief@gmail.com";
    public const string Password = "Gwynneth@1";

    public static string NormalizedEmail => Email.Trim().ToLowerInvariant();

    public static bool IsOwnerEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        email.Trim().ToLowerInvariant() == NormalizedEmail;

    public static bool MatchesPassword(string? password) =>
        password == Password;
}
