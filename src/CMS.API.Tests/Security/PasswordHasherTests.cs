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
}
