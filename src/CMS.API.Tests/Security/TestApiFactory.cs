using CMS.API.Data;
using CMS.API.Repositories;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CMS.API.Tests.Security;

/// <summary>
/// The API hosted in process, with every repository swapped for its fake. The 401 that guards a
/// protected endpoint is produced by middleware rather than by a controller, so it is only
/// observable through the real pipeline — this is the one place the tests need a host instead of a
/// controller instance.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Backs the protected endpoint the tests read from and write to.</summary>
    public FakeAppRoleRepository AppRoles { get; } = new();

    /// <summary>Backs POST /api/auth/login.</summary>
    public FakeAuthRepository Auth { get; } = new();

    /// <summary>Backs PUT /api/auth/profile, and the 使用者 AppUser endpoints.</summary>
    public FakeAppUserRepository AppUsers { get; } = new();

    /// <summary>Supplies the signing key both the issuer and the validator read.</summary>
    public FakeSysConfigRepository SysConfig { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: that turns on service-graph validation, which the swapped registrations
        // below have no reason to satisfy.
        builder.UseEnvironment(Environments.Staging);

        builder.ConfigureServices(services =>
        {
            // Nothing may reach SQL Server. A repository someone forgets to replace fails loudly
            // here rather than quietly opening a connection to the developer's database.
            Replace<IDbConnectionFactory>(services, new ThrowingDbConnectionFactory());

            Replace<IAppRoleRepository>(services, AppRoles);
            Replace<IAppUserRepository>(services, AppUsers);
            Replace<IAuthRepository>(services, Auth);
            Replace<ISysConfigRepository>(services, SysConfig);

            // In the API both repositories read the same AppUser row, so a password change is
            // visible to the token-freshness check. The fakes hold separate stores; this restores
            // the link, and is what lets a test prove an old token stops working after a change.
            Auth.PasswordUpdatedTimeSource = AppUsers.PasswordUpdatedTimeOf;

            // The rest are here so an authenticated request to any controller can reach its action
            // and prove it was not the authorization middleware that answered.
            Replace<IPublishStatusRepository>(services, new FakePublishStatusRepository());
            Replace<IPartnerRepository>(services, new FakePartnerRepository());
            Replace<ICourseGroupRepository>(services, new FakeCourseGroupRepository());
            Replace<ICourseRepository>(services, new FakeCourseRepository());
            Replace<IFeaturedPromoItemRepository>(services, new FakeFeaturedPromoItemRepository());
            Replace<ILookupRepository>(services, new FakeLookupRepository());
        });
    }

    private static void Replace<TService>(IServiceCollection services, TService instance)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddSingleton(instance);
    }
}
