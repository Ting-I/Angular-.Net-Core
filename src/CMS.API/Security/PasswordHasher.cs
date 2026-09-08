using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Security;

/// <summary>
/// Hashing for AppUser.PasswordHash.
///
/// SHA-256 hex, unsalted and uniterated. That is what the schema can hold: PasswordHash is a bare
/// nvarchar(800) with no companion salt or work-factor column, and database/*.sql is read-only
/// reference. Unsalted SHA-256 is not a password-storage primitive — moving to PBKDF2 or bcrypt
/// needs either a schema change or an encoded composite value, and is left as follow-up.
/// </summary>
public static class PasswordHasher
{
    /// <summary>SHA-256 of the UTF-8 bytes, as 64 lowercase hex characters.</summary>
    public static string Sha256Hex(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// True when the password hashes to the stored value. Hex case is ignored — rows written by
    /// hand or by an older tool may hold uppercase — and the comparison is fixed-time so a login
    /// cannot be timed character by character.
    /// </summary>
    public static bool Matches(string? password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var computed = Encoding.UTF8.GetBytes(Sha256Hex(password));
        var stored = Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant());

        // FixedTimeEquals needs equal lengths; a length mismatch is already a mismatch.
        return computed.Length == stored.Length && CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
