using System.Security.Claims;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Security;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class AppUsersControllerTests
{
    private const string DefaultPassword = "Uwa@2026";

    private static AppUser User(
        string userId,
        string userName,
        bool isActive = true,
        DateTime? passwordUpdatedTime = null,
        params string[] roleIds)
        => new()
        {
            Pkid = Math.Abs(userId.GetHashCode() % 1000),
            UserId = userId,
            UserName = userName,
            IsActive = isActive,
            PasswordUpdatedTime = passwordUpdatedTime,
            RoleIds = [.. roleIds],
            RoleCount = roleIds.Length,
        };

    private static (AppUsersController Controller, FakeAppUserRepository Repository, FakeSysConfigRepository SysConfig)
        CreateController(params AppUser[] seed)
        => CreateControllerSignedInAs(null, seed);

    /// <summary>
    /// The controller with a signed-in operator behind it, for the two guards that ask who the
    /// caller is. A null <paramref name="callerUserId"/> leaves the principal unset, which is what
    /// every other test here wants: the endpoints under it act on the record, not on the caller.
    /// </summary>
    private static (AppUsersController Controller, FakeAppUserRepository Repository, FakeSysConfigRepository SysConfig)
        CreateControllerSignedInAs(string? callerUserId, params AppUser[] seed)
    {
        var repository = new FakeAppUserRepository().Seed(seed);
        var sysConfig = new FakeSysConfigRepository { DefaultPassword = DefaultPassword };
        var controller = new AppUsersController(repository, sysConfig);

        if (callerUserId is not null)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(JwtTokenService.UserIdClaimType, callerUserId)],
                        "Bearer")),
                },
            };
        }

        return (controller, repository, sysConfig);
    }

    private static ProblemDetails AssertStatus(IActionResult result, int statusCode)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(statusCode, objectResult.StatusCode);
        return Assert.IsType<ProblemDetails>(objectResult.Value);
    }

    private static ProblemDetails AssertStatus<T>(ActionResult<T> result, int statusCode) =>
        AssertStatus(result.Result!, statusCode);

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsAllUsersOrderedByUserId()
    {
        var (controller, _, _) = CreateController(
            User("miles", "Miles Sun"),
            User("helen", "Helen Lin"));

        var users = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(["helen", "miles"], users.Select(u => u.UserId));
    }

    [Fact]
    public async Task GetAll_IncludesRoleCount()
    {
        var (controller, _, _) = CreateController(User("helen", "Helen Lin", true, null, "Admin", "Editor"));

        var users = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(2, Assert.Single(users).RoleCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithoutFilters_ReturnsEverything()
    {
        var (controller, _, _) = CreateController(User("helen", "Helen Lin"), User("miles", "Miles Sun"));

        var users = AssertOk(await controller.Query(new AppUserQuery(), CancellationToken.None));

        Assert.Equal(2, users.Count());
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesUserName()
    {
        var (controller, _, _) = CreateController(User("helen", "Helen Lin"), User("miles", "Miles Sun"));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { Keyword = "miles sun" }, CancellationToken.None));

        Assert.Equal("miles", Assert.Single(users).UserId);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesUserId()
    {
        var (controller, _, _) = CreateController(User("helen", "Helen Lin"), User("miles", "Miles Sun"));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { Keyword = "hele" }, CancellationToken.None));

        Assert.Equal("helen", Assert.Single(users).UserId);
    }

    [Fact]
    public async Task Query_WithIsActiveTrue_ReturnsOnlyActiveUsers()
    {
        var (controller, _, _) = CreateController(
            User("helen", "Helen Lin"),
            User("retired", "Retired User", isActive: false));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { IsActive = true }, CancellationToken.None));

        Assert.Equal("helen", Assert.Single(users).UserId);
    }

    /// <summary>停用 is a filter, not an absence of one.</summary>
    [Fact]
    public async Task Query_WithIsActiveFalse_ReturnsOnlyInactiveUsers()
    {
        var (controller, _, _) = CreateController(
            User("helen", "Helen Lin"),
            User("retired", "Retired User", isActive: false));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { IsActive = false }, CancellationToken.None));

        Assert.Equal("retired", Assert.Single(users).UserId);
    }

    [Fact]
    public async Task Query_WithRoleId_ReturnsHoldersOfThatRoleOnce()
    {
        var (controller, _, _) = CreateController(
            User("helen", "Helen Lin", true, null, "Admin", "Editor"),
            User("miles", "Miles Sun", true, null, "Editor"));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { RoleId = "Admin" }, CancellationToken.None));

        Assert.Equal("helen", Assert.Single(users).UserId);
    }

    [Fact]
    public async Task Query_WithPasswordUpdatedRange_FiltersOnBothBounds()
    {
        var (controller, _, _) = CreateController(
            User("early", "Early", true, new DateTime(2025, 12, 31, 23, 0, 0, DateTimeKind.Utc)),
            User("inside", "Inside", true, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)),
            User("late", "Late", true, new DateTime(2026, 2, 1, 0, 30, 0, DateTimeKind.Utc)));

        var users = AssertOk(await controller.Query(
            new AppUserQuery
            {
                PasswordUpdatedFrom = new DateOnly(2026, 1, 1),
                PasswordUpdatedTo = new DateOnly(2026, 1, 31),
            },
            CancellationToken.None));

        Assert.Equal("inside", Assert.Single(users).UserId);
    }

    /// <summary>The upper bound covers the whole closing day, not just midnight on it.</summary>
    [Fact]
    public async Task Query_WithPasswordUpdatedTo_IncludesTheWholeClosingDay()
    {
        var (controller, _, _) = CreateController(
            User("late", "Late", true, new DateTime(2026, 1, 31, 23, 59, 0, DateTimeKind.Utc)));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { PasswordUpdatedTo = new DateOnly(2026, 1, 31) }, CancellationToken.None));

        Assert.Single(users);
    }

    [Fact]
    public async Task Query_WithPasswordUpdatedRange_DropsUsersThatNeverSetAPassword()
    {
        var (controller, _, _) = CreateController(User("never", "Never", true, null));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { PasswordUpdatedFrom = new DateOnly(2026, 1, 1) }, CancellationToken.None));

        Assert.Empty(users);
    }

    [Fact]
    public async Task Query_WithCombinedFilters_AppliesAll()
    {
        var (controller, _, _) = CreateController(
            User("helen", "Helen Lin", true, null, "Admin"),
            User("helper", "Helper Bot", false, null, "Admin"));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { Keyword = "hel", IsActive = false, RoleId = "Admin" },
            CancellationToken.None));

        Assert.Equal("helper", Assert.Single(users).UserId);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var (controller, _, _) = CreateController(User("helen", "Helen Lin"));

        var users = AssertOk(await controller.Query(
            new AppUserQuery { Keyword = "nope" }, CancellationToken.None));

        Assert.Empty(users);
    }

    [Fact]
    public async Task Query_WithNullBody_ReturnsEverything()
    {
        var (controller, _, _) = CreateController(User("helen", "Helen Lin"));

        var users = AssertOk(await controller.Query(null!, CancellationToken.None));

        Assert.Single(users);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsUser()
    {
        var (controller, _, _) = CreateController(
            User("helen", "Helen Lin", true, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc), "Admin"));

        var user = AssertOk(await controller.GetById("helen", CancellationToken.None));

        Assert.Equal("Helen Lin", user.UserName);
        Assert.True(user.IsActive);
        Assert.Equal(["Admin"], user.RoleIds);
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetById("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ReturnsCreatedAtActionWithUserIdRouteValue()
    {
        var (controller, repository, _) = CreateController();

        var result = await controller.Create(
            new AppUserRequest { UserId = "helen", UserName = "Helen Lin" }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AppUsersController.GetById), created.ActionName);
        Assert.Equal("helen", created.RouteValues!["id"]);
        Assert.Equal(["helen"], repository.CreatedUserIds);

        var user = Assert.IsType<AppUser>(created.Value);
        Assert.Equal("Helen Lin", user.UserName);
    }

    [Fact]
    public async Task Create_PersistsSelectedRoles()
    {
        var (controller, repository, _) = CreateController();

        await controller.Create(
            new AppUserRequest { UserId = "helen", UserName = "Helen Lin", RoleIds = ["Admin", "Editor"] },
            CancellationToken.None);

        var stored = await repository.GetByIdAsync("helen");
        Assert.Equal(["Admin", "Editor"], stored!.RoleIds);
        Assert.Equal(2, stored.RoleCount);
    }

    /// <summary>The SysConfig default password, hashed — never a value carried on the request.</summary>
    [Fact]
    public async Task Create_StoresTheHashedSysConfigDefaultPassword()
    {
        var (controller, repository, sysConfig) = CreateController();

        await controller.Create(
            new AppUserRequest { UserId = "helen", UserName = "Helen Lin" }, CancellationToken.None);

        Assert.Equal(1, sysConfig.GetDefaultPasswordCallCount);
        Assert.True(PasswordHasher.Matches(DefaultPassword, repository.PasswordHashOf("helen")));
        Assert.NotEqual(DefaultPassword, repository.PasswordHashOf("helen"));
    }

    [Fact]
    public async Task Create_StampsPasswordUpdatedTime()
    {
        var (controller, repository, _) = CreateController();

        await controller.Create(
            new AppUserRequest { UserId = "helen", UserName = "Helen Lin" }, CancellationToken.None);

        var stored = await repository.GetByIdAsync("helen");
        Assert.Equal(repository.UtcNow, stored!.PasswordUpdatedTime);
    }

    [Fact]
    public async Task Create_WithDuplicateUserId_Returns409()
    {
        var (controller, repository, _) = CreateController(User("helen", "Helen Lin"));

        var result = await controller.Create(
            new AppUserRequest { UserId = "helen", UserName = "Dup" }, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Empty(repository.CreatedUserIds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Create_WhenTheDefaultPasswordIsUnavailable_Returns500AndCreatesNothing(string? configured)
    {
        var (controller, repository, sysConfig) = CreateController();
        sysConfig.DefaultPassword = configured;

        var result = await controller.Create(
            new AppUserRequest { UserId = "helen", UserName = "Helen Lin" }, CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(error.Value);
        Assert.Equal("系統設定缺少預設密碼", problem.Title);
        Assert.Empty(repository.CreatedUserIds);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_TakesKeyFromBodyAndReturnsUpdatedUser()
    {
        var (controller, repository, _) = CreateController(User("helen", "Helen Lin"));

        var result = await controller.Update(
            new AppUserRequest { UserId = "helen", UserName = "林海倫", IsActive = false },
            CancellationToken.None);

        var user = AssertOk(result);
        Assert.Equal("林海倫", user.UserName);
        Assert.False(user.IsActive);
        Assert.Equal(["helen"], repository.UpdatedUserIds);
    }

    [Fact]
    public async Task Update_ReplacesRoleAssignments()
    {
        var (controller, repository, _) = CreateController(User("helen", "Helen Lin"));

        await controller.Update(
            new AppUserRequest { UserId = "helen", UserName = "Helen Lin", RoleIds = ["Admin", "Editor"] },
            CancellationToken.None);
        await controller.Update(
            new AppUserRequest { UserId = "helen", UserName = "Helen Lin", RoleIds = ["Editor"] },
            CancellationToken.None);

        var stored = await repository.GetByIdAsync("helen");
        Assert.Equal(["Editor"], stored!.RoleIds);
    }

    /// <summary>The whole of the do-not-modify-PasswordHash-on-update rule.</summary>
    [Fact]
    public async Task Update_LeavesPasswordHashAndTimestampUntouched()
    {
        var stamped = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var (controller, repository, sysConfig) = CreateController(User("helen", "Helen Lin", true, stamped));
        var hashBefore = repository.PasswordHashOf("helen");

        await controller.Update(
            new AppUserRequest { UserId = "helen", UserName = "林海倫" }, CancellationToken.None);

        Assert.Equal(hashBefore, repository.PasswordHashOf("helen"));
        Assert.Equal(stamped, (await repository.GetByIdAsync("helen"))!.PasswordUpdatedTime);
        Assert.Equal(0, sysConfig.GetDefaultPasswordCallCount);
    }

    [Fact]
    public async Task Update_WhenMissing_Returns404()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.Update(
            new AppUserRequest { UserId = "missing", UserName = "x" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Reset password ----------

    [Fact]
    public async Task ResetPassword_WritesANewHashAndTimestampAndReturns204()
    {
        var stamped = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var (controller, repository, _) = CreateController(User("helen", "Helen Lin", true, stamped));

        var result = await controller.ResetPassword("helen", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(["helen"], repository.ResetUserIds);
        Assert.True(PasswordHasher.Matches(DefaultPassword, repository.PasswordHashOf("helen")));
        Assert.Equal(repository.UtcNow, (await repository.GetByIdAsync("helen"))!.PasswordUpdatedTime);
    }

    [Fact]
    public async Task ResetPassword_DoesNotChangeAnyOtherField()
    {
        var (controller, repository, _) = CreateController(User("helen", "Helen Lin", true, null, "Admin"));

        await controller.ResetPassword("helen", CancellationToken.None);

        var stored = await repository.GetByIdAsync("helen");
        Assert.Equal("Helen Lin", stored!.UserName);
        Assert.True(stored.IsActive);
        Assert.Equal(["Admin"], stored.RoleIds);
    }

    [Fact]
    public async Task ResetPassword_WhenUserIsMissing_Returns404WithoutReadingSysConfig()
    {
        var (controller, repository, sysConfig) = CreateController();

        var result = await controller.ResetPassword("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(repository.ResetUserIds);
        Assert.Equal(0, sysConfig.GetDefaultPasswordCallCount);
    }

    [Fact]
    public async Task ResetPassword_WhenTheDefaultPasswordIsUnavailable_Returns500()
    {
        var (controller, repository, sysConfig) = CreateController(User("helen", "Helen Lin"));
        sysConfig.DefaultPassword = null;
        var hashBefore = repository.PasswordHashOf("helen");

        var result = await controller.ResetPassword("helen", CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(error.Value);
        Assert.Equal("系統設定缺少預設密碼", problem.Title);
        Assert.Equal(hashBefore, repository.PasswordHashOf("helen"));
        Assert.Empty(repository.ResetUserIds);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesUserAndReturns204()
    {
        var (controller, repository, _) = CreateController(User("helen", "Helen Lin", true, null, "Admin"));

        var result = await controller.Delete("helen", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync("helen"));
    }

    [Fact]
    public async Task Delete_WhenMissing_Returns404()
    {
        var (controller, _, _) = CreateController();

        Assert.IsType<NotFoundResult>(await controller.Delete("missing", CancellationToken.None));
    }

    // ---------- Guards on the caller's own account ----------
    //
    // The controller is behind the Admin policy, so everything below is an administrator acting on
    // themselves. Holding the role is not licence to hand yourself another one, or to put your own
    // account on the weakest password in the system.

    [Fact]
    public async Task Update_WhenTheCallerChangesTheirOwnRoles_Returns403AndWritesNothing()
    {
        var (controller, repository, _) = CreateControllerSignedInAs(
            "helen",
            User("helen", "Helen Lin", true, null, "Editor"));

        var problem = AssertStatus(
            await controller.Update(
                new AppUserRequest { UserId = "helen", UserName = "Helen Lin", RoleIds = ["Editor", "Admin"] },
                CancellationToken.None),
            StatusCodes.Status403Forbidden);

        Assert.Equal("無法變更自己的角色", problem.Title);
        Assert.Empty(repository.UpdatedUserIds);
        Assert.Equal(["Editor"], (await repository.GetByIdAsync("helen"))!.RoleIds);
    }

    [Fact]
    public async Task Update_WhenTheCallerRemovesTheirOwnLastRole_Returns403()
    {
        // The other half of the same guard: stripping your own Admin role would lock the
        // sub-system away from everybody, this account included.
        var (controller, repository, _) = CreateControllerSignedInAs(
            "helen",
            User("helen", "Helen Lin", true, null, "Admin"));

        AssertStatus(
            await controller.Update(
                new AppUserRequest { UserId = "helen", UserName = "Helen Lin", RoleIds = [] },
                CancellationToken.None),
            StatusCodes.Status403Forbidden);

        Assert.Equal(["Admin"], (await repository.GetByIdAsync("helen"))!.RoleIds);
    }

    [Fact]
    public async Task Update_WhenTheCallerKeepsTheirOwnRoles_Succeeds()
    {
        // Renaming yourself is an ordinary edit. Order and case must not read as a change — the
        // form round-trips whatever the list gave it.
        var (controller, repository, _) = CreateControllerSignedInAs(
            "helen",
            User("helen", "Helen Lin", true, null, "Admin", "Editor"));

        var updated = AssertOk(await controller.Update(
            new AppUserRequest { UserId = "helen", UserName = "Helen Chen", RoleIds = ["editor", "ADMIN"] },
            CancellationToken.None));

        Assert.Equal("Helen Chen", updated.UserName);
        Assert.Equal(["helen"], repository.UpdatedUserIds);
    }

    [Fact]
    public async Task Update_WhenTheCallerChangesSomebodyElsesRoles_Succeeds()
    {
        // The guard is about the caller's own account and nothing else — this is the endpoint's job.
        var (controller, repository, _) = CreateControllerSignedInAs(
            "helen",
            User("helen", "Helen Lin", true, null, "Admin"),
            User("miles", "Miles Sun", true, null, "Editor"));

        AssertOk(await controller.Update(
            new AppUserRequest { UserId = "miles", UserName = "Miles Sun", RoleIds = ["Admin"] },
            CancellationToken.None));

        Assert.Equal(["Admin"], (await repository.GetByIdAsync("miles"))!.RoleIds);
    }

    [Fact]
    public async Task Update_MatchesTheCallerCaseInsensitively()
    {
        // SQL Server's default collation is case-insensitive and every lookup on the key follows
        // it; a guard "Helen" walked past as "helen" would be no guard.
        var (controller, _, _) = CreateControllerSignedInAs(
            "helen",
            User("Helen", "Helen Lin", true, null, "Editor"));

        AssertStatus(
            await controller.Update(
                new AppUserRequest { UserId = "Helen", UserName = "Helen Lin", RoleIds = ["Admin"] },
                CancellationToken.None),
            StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ResetPassword_WhenTheCallerTargetsTheirOwnAccount_Returns403AndWritesNothing()
    {
        var (controller, repository, sysConfig) = CreateControllerSignedInAs(
            "helen",
            User("helen", "Helen Lin"));
        var before = repository.PasswordHashOf("helen");

        var problem = AssertStatus(
            await controller.ResetPassword("helen", CancellationToken.None),
            StatusCodes.Status403Forbidden);

        Assert.Equal("無法重設自己的密碼，請使用變更密碼", problem.Title);
        Assert.Empty(repository.ResetUserIds);
        Assert.Equal(before, repository.PasswordHashOf("helen"));

        // Refused before the default password is even read.
        Assert.Equal(0, sysConfig.GetDefaultPasswordCallCount);
    }

    [Fact]
    public async Task ResetPassword_WhenTheCallerTargetsAnotherAccount_Succeeds()
    {
        var (controller, repository, _) = CreateControllerSignedInAs(
            "helen",
            User("helen", "Helen Lin"),
            User("miles", "Miles Sun"));

        Assert.IsType<NoContentResult>(await controller.ResetPassword("miles", CancellationToken.None));
        Assert.Equal(["miles"], repository.ResetUserIds);
    }
}
