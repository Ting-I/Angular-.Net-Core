using CMS.API.Security;

namespace CMS.API.Tests.Security;

/// <summary>
/// The new-password complexity rule on its own: at least 8 characters drawn from at least three of
/// the four classes. Static logic with no fake and no database behind it, tested directly the way
/// the {Table}Sql.BuildWhere suites are.
/// </summary>
public class PasswordPolicyTests
{
    [Theory]
    // Exactly three classes, one combination each — every one of these must pass.
    [InlineData("Abcdefg1")]   // upper + lower + digit
    [InlineData("Abcdefg!")]   // upper + lower + symbol
    [InlineData("ABCDEF1!")]   // upper + digit + symbol
    [InlineData("abcdef1!")]   // lower + digit + symbol
    // All four, including the SysConfig defaultPassword the fakes sign in with.
    [InlineData("Uwa@2026")]
    [InlineData("Passw0rd!Passw0rd!")]
    public void IsAcceptable_AcceptsAPasswordMeetingBothHalvesOfTheRule(string password)
    {
        Assert.True(PasswordPolicy.IsAcceptable(password));
    }

    [Theory]
    [InlineData("Abc123!")]    // 7 characters, four classes — length is the failure
    [InlineData("Ab1!")]
    [InlineData("A1!")]
    [InlineData("")]
    public void IsAcceptable_RejectsAnythingShorterThanEight(string password)
    {
        Assert.False(PasswordPolicy.IsAcceptable(password));
    }

    [Theory]
    [InlineData("abcdefgh")]        // lower alone
    [InlineData("ABCDEFGH")]        // upper alone
    [InlineData("12345678")]        // digit alone
    [InlineData("!!!!!!!!")]        // symbol alone
    [InlineData("Abcdefgh")]        // upper + lower
    [InlineData("abcdefg1")]        // lower + digit
    [InlineData("abcdefg!")]        // lower + symbol
    [InlineData("ABCDEFG1")]        // upper + digit
    [InlineData("12345678!")]       // digit + symbol
    public void IsAcceptable_RejectsFewerThanThreeCharacterClasses(string password)
    {
        Assert.False(PasswordPolicy.IsAcceptable(password));
    }

    [Fact]
    public void IsAcceptable_RejectsNull()
    {
        Assert.False(PasswordPolicy.IsAcceptable(null));
    }

    [Fact]
    public void IsAcceptable_CountsLengthInCharactersNotBytes()
    {
        // Eight characters, three classes — nothing here is measured in UTF-8 bytes.
        Assert.True(PasswordPolicy.IsAcceptable("Abcdefg1"));
    }

    [Fact]
    public void IsAcceptable_TreatsACaselessScriptAsTheSymbolClass()
    {
        // 密碼 is neither upper nor lower nor a digit, so it lands in the fourth class alongside
        // punctuation: 密碼 + abc + 123 is three classes over eight characters.
        Assert.True(PasswordPolicy.IsAcceptable("密碼abc123"));

        // ...and on its own it is still only one class.
        Assert.False(PasswordPolicy.IsAcceptable("密碼密碼密碼密碼"));
    }

    [Fact]
    public void IsAcceptable_CountsWhitespaceAsTheSymbolClass()
    {
        // A passphrase's spaces are the third class here; refusing them would be a surprise.
        Assert.True(PasswordPolicy.IsAcceptable("open the door1"));
    }

    [Fact]
    public void RequirementMessage_IsTheWordingTheUiShows()
    {
        Assert.Equal(
            "密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號",
            PasswordPolicy.RequirementMessage);
    }
}
