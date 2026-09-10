using CMS.API.Repositories;

namespace CMS.API.Tests.Repositories;

/// <summary>
/// Covers pulling defaultPassword out of the SysConfig 'appConfig' JSON object. Every failure mode
/// returns null so the controller can answer with one 500 rather than throwing.
/// </summary>
public class SysConfigJsonTests
{
    [Fact]
    public void ExtractDefaultPassword_ReadsTheProperty()
    {
        var value = SysConfigRepository.ExtractDefaultPassword(
            """{"defaultPassword":"Uwa@2026","sessionMinutes":30}""");

        Assert.Equal("Uwa@2026", value);
    }

    [Fact]
    public void ExtractDefaultPassword_MatchesThePropertyNameCaseInsensitively()
    {
        Assert.Equal("Uwa@2026", SysConfigRepository.ExtractDefaultPassword("""{"DefaultPassword":"Uwa@2026"}"""));
        Assert.Equal("Uwa@2026", SysConfigRepository.ExtractDefaultPassword("""{"defaultpassword":"Uwa@2026"}"""));
    }

    [Fact]
    public void ExtractDefaultPassword_IgnoresOtherProperties()
    {
        var value = SysConfigRepository.ExtractDefaultPassword(
            """{"siteName":"UWA","defaultPassword":"Secret1","sessionMinutes":30}""");

        Assert.Equal("Secret1", value);
    }

    [Fact]
    public void ExtractDefaultPassword_WhenPropertyIsAbsent_ReturnsNull()
    {
        Assert.Null(SysConfigRepository.ExtractDefaultPassword("""{"sessionMinutes":30}"""));
    }

    [Theory]
    [InlineData("""{"defaultPassword":""}""")]
    [InlineData("""{"defaultPassword":"   "}""")]
    public void ExtractDefaultPassword_WhenPropertyIsBlank_ReturnsNull(string configValue)
    {
        Assert.Null(SysConfigRepository.ExtractDefaultPassword(configValue));
    }

    [Fact]
    public void ExtractDefaultPassword_WhenPropertyIsNotAString_ReturnsNull()
    {
        Assert.Null(SysConfigRepository.ExtractDefaultPassword("""{"defaultPassword":12345}"""));
        Assert.Null(SysConfigRepository.ExtractDefaultPassword("""{"defaultPassword":null}"""));
    }

    [Fact]
    public void ExtractDefaultPassword_WhenJsonIsMalformed_ReturnsNull()
    {
        Assert.Null(SysConfigRepository.ExtractDefaultPassword("""{"defaultPassword":"""));
        Assert.Null(SysConfigRepository.ExtractDefaultPassword("not json at all"));
    }

    [Fact]
    public void ExtractDefaultPassword_WhenJsonIsNotAnObject_ReturnsNull()
    {
        Assert.Null(SysConfigRepository.ExtractDefaultPassword("""["Uwa@2026"]"""));
        Assert.Null(SysConfigRepository.ExtractDefaultPassword("\"just a string\""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractDefaultPassword_WhenTheRowIsMissingOrEmpty_ReturnsNull(string? configValue)
    {
        Assert.Null(SysConfigRepository.ExtractDefaultPassword(configValue));
    }

    [Fact]
    public void ConfigKeyAndPropertyName_MatchTheSpec()
    {
        Assert.Equal("appConfig", SysConfigRepository.AppConfigKey);
        Assert.Equal("defaultPassword", SysConfigRepository.DefaultPasswordProperty);
    }

    // ---------- symmetricSecurityKey (the JWT signing secret) ----------

    [Fact]
    public void ExtractSymmetricSecurityKey_ReadsTheProperty()
    {
        var value = SysConfigRepository.ExtractSymmetricSecurityKey(
            """{"defaultPassword":"Uwa@2026","symmetricSecurityKey":"cloud4fun#123456cloud4fun#123456"}""");

        Assert.Equal("cloud4fun#123456cloud4fun#123456", value);
    }

    [Fact]
    public void ExtractSymmetricSecurityKey_MatchesThePropertyNameCaseInsensitively()
    {
        Assert.Equal("k", SysConfigRepository.ExtractSymmetricSecurityKey("""{"SymmetricSecurityKey":"k"}"""));
    }

    [Fact]
    public void ExtractSymmetricSecurityKey_DoesNotFallBackToAnotherProperty()
    {
        Assert.Null(SysConfigRepository.ExtractSymmetricSecurityKey("""{"defaultPassword":"Uwa@2026"}"""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("""{"symmetricSecurityKey":""}""")]
    [InlineData("""{"symmetricSecurityKey":12345}""")]
    [InlineData("""["cloud4fun#123456cloud4fun#123456"]""")]
    public void ExtractSymmetricSecurityKey_WhenUnusable_ReturnsNull(string? configValue)
    {
        Assert.Null(SysConfigRepository.ExtractSymmetricSecurityKey(configValue));
    }

    [Fact]
    public void SigningKeyPropertyName_MatchesTheSpec()
    {
        Assert.Equal("symmetricSecurityKey", SysConfigRepository.SymmetricSecurityKeyProperty);
    }
}
