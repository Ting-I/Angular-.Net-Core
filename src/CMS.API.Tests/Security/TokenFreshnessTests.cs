using CMS.API.Models;
using CMS.API.Security;

namespace CMS.API.Tests.Security;

/// <summary>
/// The rule that decides whether the account behind a token still backs it: the row is gone, the
/// account is disabled, or the token's `iat` predates AppUser.PasswordUpdatedTime. Static logic
/// with no host and no database behind it, tested directly; <c>TokenRevocationTests</c> and
/// <c>ChangePasswordTests</c> cover the same three through the real pipeline.
/// </summary>
public class TokenFreshnessTests
{
    private static DateTime Utc(int hour, int minute, int second, int millisecond = 0) =>
        new(2026, 9, 8, hour, minute, second, millisecond, DateTimeKind.Utc);

    private static AppUserTokenState State(bool isActive, DateTime? passwordUpdatedTime = null) =>
        new() { IsActive = isActive, PasswordUpdatedTime = passwordUpdatedTime };

    // ---------- IsRefused: the whole rule ----------

    [Fact]
    public void IsRefused_IsTrueWhenThereIsNoSuchAccount()
    {
        // A null state is the repository saying "no AppUser row". Nothing else in the API looks the
        // caller up, so before this a deleted operator kept full CRUD for the rest of the day.
        Assert.True(TokenFreshness.IsRefused(null, Utc(9, 0, 0)));
    }

    [Fact]
    public void IsRefused_IsTrueWhenTheAccountIsDisabled()
    {
        Assert.True(TokenFreshness.IsRefused(State(isActive: false), Utc(9, 0, 0)));
    }

    [Fact]
    public void IsRefused_IsFalseForAnEnabledAccountWhosePasswordNeverChanged()
    {
        Assert.False(TokenFreshness.IsRefused(State(isActive: true), Utc(9, 0, 0)));
    }

    [Fact]
    public void IsRefused_StillAppliesTheStaleCheckToAnEnabledAccount()
    {
        var state = State(isActive: true, Utc(10, 0, 0));

        Assert.True(TokenFreshness.IsRefused(state, Utc(9, 0, 0)));
        Assert.False(TokenFreshness.IsRefused(state, Utc(11, 0, 0)));
    }

    [Fact]
    public void IsRefused_RefusesADisabledAccountEvenWhenTheTokenIsNewerThanTheChange()
    {
        // 啟用 is its own answer. A token whose iat beats every password change would pass the
        // stale comparison outright, which is exactly how disabling an account did nothing.
        Assert.True(TokenFreshness.IsRefused(State(isActive: false, Utc(10, 0, 0)), Utc(11, 0, 0)));
    }

    [Fact]
    public void IsRefused_RefusesAMissingRowWhateverTheTokenSays()
    {
        // Including a token with no readable iat, and one that would otherwise be perfectly fresh.
        Assert.True(TokenFreshness.IsRefused(null, null));
        Assert.True(TokenFreshness.IsRefused(null, DateTime.UtcNow));
    }

    // ---------- IsStale: the password-clock comparison on its own ----------

    [Fact]
    public void IsStale_IsTrueForATokenIssuedBeforeTheChange()
    {
        Assert.True(TokenFreshness.IsStale(Utc(9, 0, 0), Utc(10, 0, 0)));
    }

    [Fact]
    public void IsStale_IsFalseForATokenIssuedAfterTheChange()
    {
        Assert.False(TokenFreshness.IsStale(Utc(11, 0, 0), Utc(10, 0, 0)));
    }

    [Fact]
    public void IsStale_IsFalseWhenThePasswordHasNeverBeenChanged()
    {
        // Nothing to measure the token against; the endpoint answers for itself.
        Assert.False(TokenFreshness.IsStale(Utc(9, 0, 0), null));
    }

    [Fact]
    public void IsStale_IsTrueForATokenWithNoIssuedAtOnceThereIsAChangeToJudgeIt_Against()
    {
        // Fails closed: JwtTokenService always stamps iat, so a token without one did not come
        // from here even though it carried a valid signature.
        Assert.True(TokenFreshness.IsStale(null, Utc(10, 0, 0)));
    }

    [Fact]
    public void IsStale_IsFalseForATokenWithNoIssuedAtWhenNothingHasChanged()
    {
        Assert.False(TokenFreshness.IsStale(null, null));
    }

    [Fact]
    public void IsStale_AcceptsTheTokenIssuedInTheSameSecondAsTheChange()
    {
        // The case that would otherwise lock the operator out entirely: the password is changed at
        // 10:00:00.400 and the next login lands at 10:00:00.900, whose iat floors to 10:00:00.
        // Truncating the stored time to the second is what lets that token through.
        Assert.False(TokenFreshness.IsStale(Utc(10, 0, 0), Utc(10, 0, 0, 400)));
    }

    [Fact]
    public void IsStale_RejectsATokenFromTheSecondBeforeTheChange()
    {
        // One second earlier is still outside the truncation window, so revocation holds.
        Assert.True(TokenFreshness.IsStale(Utc(9, 59, 59), Utc(10, 0, 0, 400)));
    }

    [Fact]
    public void IsStale_ComparesInstantsWhateverKindTheStoredValueCarries()
    {
        // Dapper reads a SQL `datetime` back as Unspecified; GETUTCDATE() means it is UTC anyway,
        // and the comparison is by instant either way.
        var storedAsUnspecified = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Unspecified);

        Assert.True(TokenFreshness.IsStale(Utc(9, 0, 0), storedAsUnspecified));
        Assert.False(TokenFreshness.IsStale(Utc(11, 0, 0), storedAsUnspecified));
    }

    [Fact]
    public void TruncateToSecond_DropsTheSubSecondPartAndKeepsTheKind()
    {
        var truncated = TokenFreshness.TruncateToSecond(Utc(10, 0, 0, 999));

        Assert.Equal(Utc(10, 0, 0), truncated);
        Assert.Equal(DateTimeKind.Utc, truncated.Kind);
    }

    [Fact]
    public void IssuedAtOf_IsNullForATokenThatIsNotAJsonWebToken()
    {
        Assert.Null(TokenFreshness.IssuedAtOf(null));
    }
}
