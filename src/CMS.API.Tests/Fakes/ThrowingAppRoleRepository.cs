using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// An <see cref="IAppRoleRepository"/> whose every member fails, standing in for the repository
/// whose Dapper call went wrong. It is the counterpart of <see cref="ThrowingDbConnectionFactory"/>:
/// that one proves nothing reaches SQL Server, this one gives the exception middleware something
/// to catch on a real request through the real pipeline.
///
/// The exception message deliberately reads like a <c>SqlException</c> — a statement, an object
/// name, a server and a database — so a test can assert that none of it reaches the caller.
/// </summary>
public class ThrowingAppRoleRepository : IAppRoleRepository
{
    /// <summary>Every token in here must be absent from the response body.</summary>
    public const string SensitiveMessage =
        "Invalid column name 'RoleNam'. " +
        "SELECT pkid, RoleId FROM dbo.SecretRoleTable; server=db-prod-01.internal; database=CmsSecrets";

    /// <summary>How many times a request actually reached the repository.</summary>
    public int Calls { get; private set; }

    private InvalidOperationException Fail()
    {
        Calls++;
        return new InvalidOperationException(SensitiveMessage);
    }

    public Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw Fail();

    public Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default)
        => throw Fail();

    public Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default)
        => throw Fail();

    public Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default)
        => throw Fail();

    public Task<string> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
        => throw Fail();

    public Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
        => throw Fail();

    public Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default)
        => throw Fail();
}
