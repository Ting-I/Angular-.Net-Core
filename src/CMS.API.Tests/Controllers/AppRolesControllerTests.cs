using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class AppRolesControllerTests
{
    private static AppRole Role(string roleId, string roleName, int level, string? description = null, int userCount = 0)
        => new()
        {
            Pkid = Math.Abs(roleId.GetHashCode() % 1000),
            RoleId = roleId,
            RoleName = roleName,
            PermissionLevel = level,
            Description = description,
            UserCount = userCount,
        };

    private static (AppRolesController Controller, FakeAppRoleRepository Repository) CreateController(
        params AppRole[] seed)
    {
        var repository = new FakeAppRoleRepository().Seed(seed);
        return (new AppRolesController(repository), repository);
    }

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsAllRolesOrderedByRoleId()
    {
        var (controller, _) = CreateController(
            Role("User", "User", 100, "一般使用者", userCount: 9),
            Role("Admin", "Administrator", 1, "系統管理員", userCount: 3));

        var roles = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(["Admin", "User"], roles.Select(r => r.RoleId));
    }

    [Fact]
    public async Task GetAll_IncludesUserCount()
    {
        var (controller, _) = CreateController(Role("Admin", "Administrator", 1, userCount: 3));

        var roles = AssertOk(await controller.GetAll(CancellationToken.None)).ToList();

        Assert.Equal(3, Assert.Single(roles).UserCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_WithoutFilters_ReturnsEverything()
    {
        var (controller, _) = CreateController(
            Role("Admin", "Administrator", 1),
            Role("User", "User", 100));

        var roles = AssertOk(await controller.Query(new AppRoleQuery(), CancellationToken.None));

        Assert.Equal(2, roles.Count());
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesRoleName()
    {
        var (controller, _) = CreateController(
            Role("Admin", "Administrator", 1),
            Role("User", "User", 100));

        var roles = AssertOk(await controller.Query(
            new AppRoleQuery { Keyword = "administrator" }, CancellationToken.None));

        Assert.Equal("Admin", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_WithKeyword_MatchesDescription()
    {
        var (controller, _) = CreateController(
            Role("Admin", "Administrator", 1, "系統管理員"),
            Role("User", "User", 100, "一般使用者"));

        var roles = AssertOk(await controller.Query(
            new AppRoleQuery { Keyword = "一般" }, CancellationToken.None));

        Assert.Equal("User", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_WithPermissionLevel_FiltersExactly()
    {
        var (controller, _) = CreateController(
            Role("Admin", "Administrator", 1),
            Role("Editor", "Editor", 50),
            Role("User", "User", 100));

        var roles = AssertOk(await controller.Query(
            new AppRoleQuery { PermissionLevel = 50 }, CancellationToken.None));

        Assert.Equal("Editor", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_WithCombinedFilters_AppliesBoth()
    {
        var (controller, _) = CreateController(
            Role("Admin", "Administrator", 1),
            Role("AdminLite", "Administrator Lite", 50));

        var roles = AssertOk(await controller.Query(
            new AppRoleQuery { Keyword = "admin", PermissionLevel = 50 }, CancellationToken.None));

        Assert.Equal("AdminLite", Assert.Single(roles).RoleId);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var (controller, _) = CreateController(Role("Admin", "Administrator", 1));

        var roles = AssertOk(await controller.Query(
            new AppRoleQuery { Keyword = "nope" }, CancellationToken.None));

        Assert.Empty(roles);
    }

    [Fact]
    public async Task Query_WithNullBody_ReturnsEverything()
    {
        var (controller, _) = CreateController(Role("Admin", "Administrator", 1));

        var roles = AssertOk(await controller.Query(null!, CancellationToken.None));

        Assert.Single(roles);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsRole()
    {
        var (controller, _) = CreateController(Role("Admin", "Administrator", 1, "系統管理員"));

        var role = AssertOk(await controller.GetById("Admin", CancellationToken.None));

        Assert.Equal("Administrator", role.RoleName);
        Assert.Equal(1, role.PermissionLevel);
        Assert.Equal("系統管理員", role.Description);
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.GetById("Missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ReturnsCreatedAtActionWithRoleIdRouteValue()
    {
        var (controller, repository) = CreateController();
        var request = new AppRoleRequest
        {
            RoleId = "Editor",
            RoleName = "Editor",
            PermissionLevel = 50,
            Description = "內容編輯",
        };

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(AppRolesController.GetById), created.ActionName);
        Assert.Equal("Editor", created.RouteValues!["id"]);
        Assert.Equal(["Editor"], repository.CreatedRoleIds);

        var role = Assert.IsType<AppRole>(created.Value);
        Assert.Equal("Editor", role.RoleId);
        Assert.Equal(50, role.PermissionLevel);
    }

    [Fact]
    public async Task Create_PersistsSelectedUsers()
    {
        var (controller, repository) = CreateController();
        var request = new AppRoleRequest
        {
            RoleId = "Editor",
            RoleName = "Editor",
            PermissionLevel = 50,
            UserIds = ["helen", "miles"],
        };

        await controller.Create(request, CancellationToken.None);

        var stored = await repository.GetByIdAsync("Editor");
        Assert.Equal(["helen", "miles"], stored!.UserIds);
        Assert.Equal(2, stored.UserCount);
    }

    [Fact]
    public async Task Create_WithDuplicateRoleId_Returns409()
    {
        var (controller, repository) = CreateController(Role("Admin", "Administrator", 1));

        var result = await controller.Create(
            new AppRoleRequest { RoleId = "Admin", RoleName = "Dup", PermissionLevel = 1 },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Empty(repository.CreatedRoleIds);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_TakesKeyFromBodyAndReturnsUpdatedRole()
    {
        var (controller, repository) = CreateController(Role("Admin", "Administrator", 1, "系統管理員"));

        var result = await controller.Update(
            new AppRoleRequest
            {
                RoleId = "Admin",
                RoleName = "系統管理者",
                PermissionLevel = 2,
                Description = "更新後描述",
            },
            CancellationToken.None);

        var role = AssertOk(result);
        Assert.Equal("系統管理者", role.RoleName);
        Assert.Equal(2, role.PermissionLevel);
        Assert.Equal("更新後描述", role.Description);
        Assert.Equal(["Admin"], repository.UpdatedRoleIds);
    }

    [Fact]
    public async Task Update_ReplacesUserAssignments()
    {
        var (controller, repository) = CreateController(Role("Admin", "Administrator", 1));
        await controller.Update(
            new AppRoleRequest { RoleId = "Admin", RoleName = "Administrator", PermissionLevel = 1, UserIds = ["a", "b"] },
            CancellationToken.None);

        await controller.Update(
            new AppRoleRequest { RoleId = "Admin", RoleName = "Administrator", PermissionLevel = 1, UserIds = ["c"] },
            CancellationToken.None);

        var stored = await repository.GetByIdAsync("Admin");
        Assert.Equal(["c"], stored!.UserIds);
    }

    [Fact]
    public async Task Update_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        var result = await controller.Update(
            new AppRoleRequest { RoleId = "Missing", RoleName = "x", PermissionLevel = 1 },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RemovesRoleAndReturns204()
    {
        var (controller, repository) = CreateController(Role("Admin", "Administrator", 1));

        var result = await controller.Delete("Admin", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repository.GetByIdAsync("Admin"));
    }

    [Fact]
    public async Task Delete_WhenMissing_Returns404()
    {
        var (controller, _) = CreateController();

        Assert.IsType<NotFoundResult>(await controller.Delete("Missing", CancellationToken.None));
    }
}
