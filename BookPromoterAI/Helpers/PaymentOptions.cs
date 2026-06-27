using System.Text;
namespace BookPromoterAI;

record PaymentMethodInput(
    string PaymentType,
    string Country,
    string Region,
    string CountryOther,
    string CardholderName,
    string CardNumber,
    string CardExpiry,
    string BankName,
    string RoutingOrSortCode,
    string Iban,
    string AccountNumber)
{
    public string ResolvedCountry =>
        Country.Equals("OTHER", StringComparison.OrdinalIgnoreCase)
            ? CountryOther.Trim()
            : PaymentOptions.CountryName(Country);
}

static class PaymentOptions
{
    public const string TypeCard = "card";
    public const string TypeBank = "bank";

    static readonly (string Code, string Name)[] Countries =
    [
        ("US", "United States"), ("CA", "Canada"), ("GB", "United Kingdom"), ("IE", "Ireland"),
        ("AU", "Australia"), ("NZ", "New Zealand"), ("DE", "Germany"), ("FR", "France"),
        ("ES", "Spain"), ("IT", "Italy"), ("NL", "Netherlands"), ("BE", "Belgium"),
        ("CH", "Switzerland"), ("AT", "Austria"), ("SE", "Sweden"), ("NO", "Norway"),
        ("DK", "Denmark"), ("FI", "Finland"), ("PL", "Poland"), ("PT", "Portugal"),
        ("IN", "India"), ("JP", "Japan"), ("KR", "South Korea"), ("SG", "Singapore"),
        ("HK", "Hong Kong"), ("MY", "Malaysia"), ("PH", "Philippines"), ("ID", "Indonesia"),
        ("TH", "Thailand"), ("VN", "Vietnam"), ("AE", "United Arab Emirates"), ("SA", "Saudi Arabia"),
        ("ZA", "South Africa"), ("NG", "Nigeria"), ("KE", "Kenya"), ("EG", "Egypt"),
        ("BR", "Brazil"), ("MX", "Mexico"), ("AR", "Argentina"), ("CL", "Chile"),
        ("CO", "Colombia"), ("PE", "Peru"), ("IL", "Israel"), ("TR", "Turkey"),
        ("RU", "Russia"), ("UA", "Ukraine"), ("RO", "Romania"), ("CZ", "Czech Republic"),
        ("HU", "Hungary"), ("GR", "Greece"), ("OTHER", "Other (enter below)")
    ];

    public static string CountryName(string code) =>
        Countries.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).Name ?? code;

    public static PaymentMethodInput Parse(IFormCollection form) => new(
        form["paymentType"].ToString().Trim().ToLowerInvariant(),
        form["country"].ToString().Trim().ToUpperInvariant(),
        form["region"].ToString().Trim(),
        form["countryOther"].ToString().Trim(),
        form["cardName"].ToString().Trim(),
        form["cardNumber"].ToString().Trim(),
        form["cardExpiry"].ToString().Trim(),
        form["bankName"].ToString().Trim(),
        form["routingOrSortCode"].ToString().Trim(),
        form["iban"].ToString().Trim(),
        form["accountNumber"].ToString().Trim());

    public static string CountrySelect(string selectedCode, string id = "country")
    {
        var sb = new StringBuilder();
        sb.Append($"""<select name="country" id="{id}" required>""");
        sb.Append("""<option value="">Select country</option>""");
        foreach (var (code, name) in Countries)
        {
            var sel = code.Equals(selectedCode, StringComparison.OrdinalIgnoreCase) ? " selected" : "";
            sb.Append($"""<option value="{H.Encode(code)}"{sel}>{H.Encode(name)}</option>""");
        }
        sb.Append("</select>");
        return sb.ToString();
    }

    public static string PaymentFieldsHtml(PaymentMethodInput? values = null, string prefix = "")
    {
        values ??= new PaymentMethodInput(TypeCard, "", "", "", "", "", "", "", "", "", "");
        var isCard = values.PaymentType != TypeBank;
        var isBank = !isCard;
        var countryOtherStyle = values.Country.Equals("OTHER", StringComparison.OrdinalIgnoreCase) ? "" : " style=\"display:none\"";
        var cardStyle = isCard ? "" : " style=\"display:none\"";
        var bankStyle = isBank ? "" : " style=\"display:none\"";
        var cardSelected = isCard ? " selected" : "";
        var bankSelected = isBank ? " selected" : "";

        var sb = new StringBuilder();
        sb.Append("""<p class="muted">Prices are in USD. We accept credit/debit cards and bank accounts from any country.</p>""");
        sb.Append("<label>Country\n").Append(CountrySelect(values.Country, prefix + "country")).Append("\n</label>\n");
        sb.Append($"""<label id="{prefix}country-other-wrap"{countryOtherStyle}>Country name\n""");
        sb.Append($"""<input name="countryOther" id="{prefix}countryOther" value="{H.Encode(values.CountryOther)}" placeholder="Your country">\n</label>\n""");
        sb.Append($"""<label>State / Province / Region (optional)\n<input name="region" value="{H.Encode(values.Region)}" placeholder="e.g. California, Bavaria, Ontario">\n</label>\n""");
        sb.Append($"""<label>Payment method\n<select name="paymentType" id="{prefix}paymentType" required>\n""");
        sb.Append($"""<option value="card"{cardSelected}>Credit or Debit Card</option>\n""");
        sb.Append($"""<option value="bank"{bankSelected}>Bank Account (wire, ACH, SEPA, local transfer)</option>\n</select>\n</label>\n""");
        sb.Append($"""<div id="{prefix}card-fields" class="payment-type-fields"{cardStyle}>\n""");
        sb.Append($"""<label>Name on card\n<input name="cardName" value="{H.Encode(values.CardholderName)}" placeholder="As shown on card">\n</label>\n""");
        sb.Append($"""<label>Card number\n<input name="cardNumber" value="{H.Encode(values.CardNumber)}" placeholder="International Visa, Mastercard, Amex, etc." inputmode="numeric" autocomplete="cc-number">\n</label>\n""");
        sb.Append($"""<label>Expiry (MM/YY)\n<input name="cardExpiry" value="{H.Encode(values.CardExpiry)}" placeholder="MM/YY" autocomplete="cc-exp">\n</label>\n</div>\n""");
        sb.Append($"""<div id="{prefix}bank-fields" class="payment-type-fields"{bankStyle}>\n""");
        sb.Append($"""<label>Account holder name\n<input name="cardName" value="{H.Encode(values.CardholderName)}" placeholder="Name on bank account">\n</label>\n""");
        sb.Append($"""<label>Bank name\n<input name="bankName" value="{H.Encode(values.BankName)}" placeholder="Your bank">\n</label>\n""");
        sb.Append($"""<label>IBAN (Europe, Middle East, and many regions)\n<input name="iban" value="{H.Encode(values.Iban)}" placeholder="e.g. GB29NWBK60161331926819">\n</label>\n""");
        sb.Append("""<p class="muted small-text">Or use account + routing/sort code below if you do not have an IBAN.</p>""");
        sb.Append($"""<label>Account number\n<input name="accountNumber" value="{H.Encode(values.AccountNumber)}" placeholder="Account number" inputmode="numeric">\n</label>\n""");
        sb.Append($"""<label>Routing / sort / BSB / branch code\n<input name="routingOrSortCode" value="{H.Encode(values.RoutingOrSortCode)}" placeholder="e.g. 021000021, 20-00-00, 062-000">\n</label>\n</div>\n""");
        sb.Append(PaymentToggleScript(prefix));
        return sb.ToString();
    }

    static string PaymentToggleScript(string prefix)
    {
        var safePrefix = prefix.Replace("\\", "\\\\").Replace("'", "\\'");
        return "<script>\n" +
            "(function() {\n" +
            "    var prefix = '" + safePrefix + "';\n" +
            "    var typeSel = document.getElementById(prefix + \"paymentType\");\n" +
            "    var countrySel = document.getElementById(prefix + \"country\");\n" +
            "    var cardFields = document.getElementById(prefix + \"card-fields\");\n" +
            "    var bankFields = document.getElementById(prefix + \"bank-fields\");\n" +
            "    var otherWrap = document.getElementById(prefix + \"country-other-wrap\");\n" +
            "    if (!typeSel) return;\n" +
            "    function syncPaymentType() {\n" +
            "        var useCard = typeSel.value === \"card\";\n" +
            "        cardFields.style.display = useCard ? \"\" : \"none\";\n" +
            "        bankFields.style.display = useCard ? \"none\" : \"\";\n" +
            "    }\n" +
            "    function syncCountry() {\n" +
            "        if (otherWrap) otherWrap.style.display = countrySel.value === \"OTHER\" ? \"\" : \"none\";\n" +
            "    }\n" +
            "    typeSel.addEventListener(\"change\", syncPaymentType);\n" +
            "    if (countrySel) countrySel.addEventListener(\"change\", syncCountry);\n" +
            "})();\n" +
            "</script>\n";
    }
}
