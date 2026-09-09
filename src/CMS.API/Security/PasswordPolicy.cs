namespace CMS.API.Security;

/// <summary>
/// The complexity rule a new password has to clear: at least 8 characters, drawn from at least
/// three of the four character classes.
///
/// The server is where this is decided. The Angular form applies the same rule so the operator is
/// told before the round trip, but that is convenience — a request reaching
/// <c>POST /api/auth/change-password</c> is checked here regardless of what any client did.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>密碼長度至少需 8 碼.</summary>
    public const int MinimumLength = 8;

    /// <summary>How many of the four classes must appear.</summary>
    public const int RequiredCharacterClasses = 3;

    /// <summary>
    /// The one message both failures answer with — too short, or too few classes. Deliberately
    /// single: naming which half failed tells an attacker holding a stolen token the shape of what
    /// is being rejected, and the operator has to satisfy both anyway.
    /// </summary>
    public const string RequirementMessage =
        "密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號";

    /// <summary>The English half of the same sentence, for the ProblemDetails Detail field.</summary>
    public const string RequirementDetail =
        "Password must be at least 8 characters and contain at least 3 of the 4 classes: " +
        "uppercase / lowercase / digit / symbol.";

    /// <summary>
    /// True when the password is long enough and spans enough character classes.
    ///
    /// "Symbol" is everything that is not an uppercase letter, a lowercase letter or a digit —
    /// punctuation and whitespace, but also a character from a script with no case, such as a
    /// 中文字. Counting those as the fourth class rather than as nothing is the lenient reading,
    /// and the only one that does not quietly refuse a passphrase an operator can actually type.
    /// </summary>
    public static bool IsAcceptable(string? password)
    {
        if (password is null || password.Length < MinimumLength)
        {
            return false;
        }

        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSymbol = false;

        foreach (var c in password)
        {
            if (char.IsUpper(c))
            {
                hasUpper = true;
            }
            else if (char.IsLower(c))
            {
                hasLower = true;
            }
            else if (char.IsDigit(c))
            {
                hasDigit = true;
            }
            else
            {
                hasSymbol = true;
            }
        }

        var classes = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);
        return classes >= RequiredCharacterClasses;
    }
}
