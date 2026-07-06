namespace BookPromoterAI;

/// <summary>Site owner credentials. Only these accounts see Owner settings.</summary>
static class OwnerAccount
{
    public const string Email = "bookpromoterai@gmail.com";
    public const string Password = "Gwynneth@1";

    /// <summary>Former primary owner — brand data is migrated off this account on startup.</summary>
    public const string LegacyPrimaryOwnerEmail = "bothamelanief@gmail.com";

    /// <summary>All owner login emails (primary first — brand data is stored under the primary account).</summary>
    public static readonly string[] Emails =
    [
        Email
    ];

    public static string NormalizedEmail => Normalize(Email);

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static bool IsOwnerEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        Emails.Any(e => Normalize(e) == Normalize(email!));

    public static bool MatchesPassword(string? password) =>
        password == Password;
}
