using CMS.API.Security;

namespace CMS.API.Tests.Security;

/// <summary>Covers the hashing used for AppUser.PasswordHash on create and on reset.</summary>
public class PasswordHasherTests
{
    [Fact]
    public void Sha256Hex_MatchesTheKnownVectorForAbc()
    {
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            PasswordHasher.Sha256Hex("abc"));
    }

    [Fact]
    public void Sha256Hex_MatchesTheKnownVectorForTheEmptyString()
    {
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            PasswordHasher.Sha256Hex(string.Empty));
    }

    [Fact]
    public void Sha256Hex_Returns64LowercaseHexCharacters()
    {
        var hash = PasswordHasher.Sha256Hex("Uwa@2026");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.All(hash, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void Sha256Hex_IsDeterministic()
    {
        Assert.Equal(PasswordHasher.Sha256Hex("Uwa@2026"), PasswordHasher.Sha256Hex("Uwa@2026"));
    }

    [Fact]
    public void Sha256Hex_DiffersForDifferentInputs()
    {
        Assert.NotEqual(PasswordHasher.Sha256Hex("Uwa@2026"), PasswordHasher.Sha256Hex("Uwa@2027"));
    }

    [Fact]
    public void Sha256Hex_HashesNonAsciiPasswordsAsUtf8()
    {
        // Guards the UTF-8 encoding choice: a non-ASCII password must still hash to 64 hex chars.
        Assert.Equal(64, PasswordHasher.Sha256Hex("密碼2026").Length);
    }

    [Fact]
    public void Sha256Hex_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Sha256Hex(null!));
    }

    // ---------- Matches (the login comparison) ----------

    [Fact]
    public void Matches_AcceptsTheHashOfTheSamePassword()
    {
        Assert.True(PasswordHasher.Matches("Uwa@2026", PasswordHasher.Sha256Hex("Uwa@2026")));
    }

    [Fact]
    public void Matches_IsCaseSensitiveOnThePassword()
    {
        Assert.False(PasswordHasher.Matches("uwa@2026", PasswordHasher.Sha256Hex("Uwa@2026")));
    }

    [Fact]
    public void Matches_IgnoresTheCaseOfTheStoredHex()
    {
        // A row written by hand or by an older tool may hold uppercase hex.
        Assert.True(PasswordHasher.Matches("Uwa@2026", PasswordHasher.Sha256Hex("Uwa@2026").ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    public void Matches_WithAnUnusableStoredHash_ReturnsFalse(string? storedHash)
    {
        Assert.False(PasswordHasher.Matches("Uwa@2026", storedHash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Matches_WithNoPassword_ReturnsFalse(string? password)
    {
        // Never let a blank password fall through to the hash of the empty string.
        Assert.False(PasswordHasher.Matches(password, PasswordHasher.Sha256Hex(string.Empty)));
    }

    // ---------- Hash (the format everything is stored in now) ----------

    [Fact]
    public void Hash_ProducesAValueThatVerifiesTheSamePassword()
    {
        Assert.True(PasswordHasher.Matches("Uwa@2026", PasswordHasher.Hash("Uwa@2026")));
    }

    [Fact]
    public void Hash_RejectsADifferentPassword()
    {
        Assert.False(PasswordHasher.Matches("Uwa@2027", PasswordHasher.Hash("Uwa@2026")));
    }

    [Fact]
    public void Hash_IsCaseSensitiveOnThePassword()
    {
        Assert.False(PasswordHasher.Matches("uwa@2026", PasswordHasher.Hash("Uwa@2026")));
    }

    /// <summary>
    /// The whole point of the salt. Two accounts on the same password must not share a stored
    /// value — otherwise the table still says which operators picked the same one, and one
    /// cracked row still cracks all of them.
    /// </summary>
    [Fact]
    public void Hash_IsDifferentEveryTimeForTheSamePassword()
    {
        var first = PasswordHasher.Hash("Uwa@2026");
        var second = PasswordHasher.Hash("Uwa@2026");

        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Matches("Uwa@2026", first));
        Assert.True(PasswordHasher.Matches("Uwa@2026", second));
    }

    [Fact]
    public void Hash_CarriesTheParametersNeededToVerifyIt()
    {
        var fields = PasswordHasher.Hash("Uwa@2026").Split(PasswordHasher.FieldSeparator);

        Assert.Equal(5, fields.Length);
        Assert.Equal(PasswordHasher.Pbkdf2Prefix, fields[0]);
        Assert.Equal(PasswordHasher.Pbkdf2PrfName, fields[1]);
        Assert.Equal(PasswordHasher.Iterations.ToString(), fields[2]);
        Assert.Equal(PasswordHasher.SaltBytes, Convert.FromBase64String(fields[3]).Length);
        Assert.Equal(PasswordHasher.SubkeyBytes, Convert.FromBase64String(fields[4]).Length);
    }

    /// <summary>
    /// The column is a read-only <c>nvarchar(800)</c> and the schema cannot change, so the encoded
    /// value has to fit with room for a later, higher iteration count.
    /// </summary>
    [Fact]
    public void Hash_FitsThePasswordHashColumn()
    {
        Assert.True(PasswordHasher.Hash("Uwa@2026").Length < 200);
    }

    [Fact]
    public void Hash_HandlesNonAsciiPasswords()
    {
        Assert.True(PasswordHasher.Matches("密碼2026", PasswordHasher.Hash("密碼2026")));
    }

    [Fact]
    public void Hash_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Hash(null!));
    }

    [Fact]
    public void Hash_UsesAtLeastTheOwaspFloorOfIterations()
    {
        Assert.True(PasswordHasher.Iterations >= 210_000);
    }

    // ---------- Reading both formats ----------

    [Fact]
    public void Matches_StillAcceptsALegacyHash()
    {
        // Rows written before the change have to keep working, or every operator is locked out.
        Assert.True(PasswordHasher.Matches("Uwa@2026", PasswordHasher.Sha256Hex("Uwa@2026")));
    }

    [Theory]
    [InlineData("PBKDF2$SHA256$210000$notbase64$notbase64")]
    [InlineData("PBKDF2$SHA256$0$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2$SHA256$210000")]
    [InlineData("PBKDF2$MD5$210000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2$SHA256$abc$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Matches_WithAMalformedComposite_ReturnsFalseRatherThanThrowing(string storedHash)
    {
        // A truncated or hand-edited row must fail the login, not the request.
        Assert.False(PasswordHasher.Matches("Uwa@2026", storedHash));
    }

    /// <summary>A legacy hash that is not 64 hex characters is a mismatch, not something to compare.</summary>
    [Theory]
    [InlineData("abc")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015")]
    public void Matches_WithATruncatedLegacyHash_ReturnsFalse(string storedHash)
    {
        Assert.False(PasswordHasher.Matches("abc", storedHash));
    }

    // ---------- NeedsUpgrade (which rows the login path rewrites) ----------

    [Fact]
    public void NeedsUpgrade_IsTrueForALegacyHash()
    {
        Assert.True(PasswordHasher.NeedsUpgrade(PasswordHasher.Sha256Hex("Uwa@2026")));
    }

    [Fact]
    public void NeedsUpgrade_IsFalseForACurrentHash()
    {
        Assert.False(PasswordHasher.NeedsUpgrade(PasswordHasher.Hash("Uwa@2026")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NeedsUpgrade_WithNothingStored_IsFalse(string? storedHash)
    {
        // There is nothing to rewrite, and no password that would verify against it either.
        Assert.False(PasswordHasher.NeedsUpgrade(storedHash));
    }
}
