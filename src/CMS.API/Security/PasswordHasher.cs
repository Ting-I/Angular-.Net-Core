using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Security;

/// <summary>
/// Hashing for AppUser.PasswordHash.
///
/// PBKDF2-HMAC-SHA256, salted per account and iterated, written as one self-describing string:
/// <c>PBKDF2$SHA256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 subkey&gt;</c>. Everything needed to
/// verify a password travels with it, so no companion salt or work-factor column is required —
/// which matters here, because <c>database/*.sql</c> is read-only reference and PasswordHash is a
/// bare <c>nvarchar(800)</c>. The encoded value is about 90 characters, so the existing column
/// holds it with room to spare, and the iteration count can be raised later without a schema
/// change or a format change: it is read back out of the stored value rather than assumed.
///
/// **The old format is still verified, and only verified.** Rows written before this carry an
/// unsalted single-iteration SHA-256 hex string, which is not a password-storage primitive: with
/// no salt, one pass of a wordlist tests every account at once. <see cref="Matches"/> therefore
/// accepts both shapes, <see cref="NeedsUpgrade"/> reports which one a row holds, and
/// <c>AuthController</c> rewrites the row in the new format on the next successful sign-in — the
/// only moment the plaintext is available to rehash. Nothing writes a SHA-256 hash any more;
/// <see cref="Sha256Hex"/> survives to read what is already stored.
/// </summary>
public static class PasswordHasher
{
    /// <summary>Marks a stored value as the PBKDF2 composite rather than the legacy hex.</summary>
    public const string Pbkdf2Prefix = "PBKDF2";

    /// <summary>The pseudo-random function name recorded in the composite.</summary>
    public const string Pbkdf2PrfName = "SHA256";

    /// <summary>Separates the composite's fields. Absent from base64 and from decimal digits.</summary>
    public const char FieldSeparator = '$';

    /// <summary>Per-account salt width. 128 bits — the length RFC 8018 calls sufficient.</summary>
    public const int SaltBytes = 16;

    /// <summary>Derived subkey width, matching the 256-bit output of the PRF.</summary>
    public const int SubkeyBytes = 32;

    /// <summary>
    /// Iterations for a newly written hash — OWASP's current floor for PBKDF2-HMAC-SHA256. Stored
    /// inside the composite, so raising this number does not invalidate rows written under the old
    /// one; they verify at their own count and are rewritten at the new one on next sign-in.
    /// </summary>
    public const int Iterations = 210_000;

    /// <summary>Length of the legacy unsalted SHA-256 hex string.</summary>
    private const int Sha256HexLength = 64;

    private static readonly HashAlgorithmName Prf = HashAlgorithmName.SHA256;

    /// <summary>
    /// The value to store for a password: a fresh random salt, the derived subkey, and the
    /// parameters both were produced under. Two calls with the same password return different
    /// strings, which is the point — equal hashes must not reveal equal passwords.
    /// </summary>
    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            Prf,
            SubkeyBytes);

        return string.Join(
            FieldSeparator,
            Pbkdf2Prefix,
            Pbkdf2PrfName,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(subkey));
    }

    /// <summary>
    /// SHA-256 of the UTF-8 bytes, as 64 lowercase hex characters — the legacy stored format.
    ///
    /// Kept so rows written before <see cref="Hash"/> can still be verified and upgraded. Nothing
    /// in the API stores its result any more.
    /// </summary>
    public static string Sha256Hex(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// True when the password verifies against the stored value, in whichever format that value
    /// holds. Every comparison is fixed-time, so a login cannot be timed byte by byte, and an
    /// unparseable or empty stored value is a mismatch rather than an exception — a malformed row
    /// must fail the login, not the request.
    /// </summary>
    public static bool Matches(string? password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var stored = storedHash.Trim();
        return IsPbkdf2(stored)
            ? MatchesPbkdf2(password, stored)
            : MatchesSha256Hex(password, stored);
    }

    /// <summary>
    /// True when the stored value is not in the current format and should be rewritten. Asked only
    /// after <see cref="Matches"/> has returned true, so it is deciding about a value that has just
    /// verified: the plaintext is in hand and this is the one moment it can be rehashed.
    /// </summary>
    public static bool NeedsUpgrade(string? storedHash) =>
        !string.IsNullOrWhiteSpace(storedHash) && !IsPbkdf2(storedHash.Trim());

    /// <summary>True when the value carries the composite's marker.</summary>
    private static bool IsPbkdf2(string storedHash) =>
        storedHash.StartsWith(Pbkdf2Prefix + FieldSeparator, StringComparison.Ordinal);

    /// <summary>
    /// Verifies against the composite, re-deriving with the salt and iteration count the stored
    /// value itself names. A field that will not parse — a truncated row, a hand-edited one, a PRF
    /// this build does not implement — is a mismatch.
    /// </summary>
    private static bool MatchesPbkdf2(string password, string storedHash)
    {
        var fields = storedHash.Split(FieldSeparator);
        if (fields.Length != 5 || !string.Equals(fields[1], Pbkdf2PrfName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            iterations <= 0)
        {
            return false;
        }

        if (!TryFromBase64(fields[3], out var salt) || !TryFromBase64(fields[4], out var expected))
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            Prf,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// Verifies against the legacy unsalted hex. Hex case is ignored — rows written by hand or by
    /// an older tool may hold uppercase — and anything that is not 64 hex characters is a mismatch
    /// rather than something to compare against.
    /// </summary>
    private static bool MatchesSha256Hex(string password, string storedHash)
    {
        if (storedHash.Length != Sha256HexLength)
        {
            return false;
        }

        var computed = Encoding.UTF8.GetBytes(Sha256Hex(password));
        var stored = Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant());

        // FixedTimeEquals needs equal lengths; a length mismatch is already a mismatch.
        return computed.Length == stored.Length && CryptographicOperations.FixedTimeEquals(computed, stored);
    }

    /// <summary>Base64 decode that answers false rather than throwing on a malformed field.</summary>
    private static bool TryFromBase64(string value, out byte[] bytes)
    {
        var buffer = new byte[((value.Length + 3) / 4) * 3];
        if (Convert.TryFromBase64String(value, buffer, out var written) && written > 0)
        {
            bytes = buffer[..written];
            return true;
        }

        bytes = [];
        return false;
    }
}
