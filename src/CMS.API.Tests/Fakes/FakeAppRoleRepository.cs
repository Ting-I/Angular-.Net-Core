using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAppRoleRepository"/> that mirrors the real repository's contract
/// (string PK, delete-then-reinsert n-n, UserCount projection) so controller behaviour can be
/// tested without SQL Server.
/// </summary>
public class FakeAppRoleRepository : IAppRoleRepository
{
    private readonly Dictionary<string, AppRole> _roles = new(StringComparer.OrdinalIgnoreCase);

    public List<string> CreatedRoleIds { get; } = [];
    public List<string> UpdatedRoleIds { get; } = [];

    /// <summary>
    /// Every RoleId the controller asked to delete, in order. Recorded so a test can prove a
    /// refused delete wrote nothing — the guard has to return *before* the repository call, since
    /// the real DeleteAsync removes the AppUserRole rows and there is no undoing that.
    /// </summary>
    public List<string> DeletedRoleIds { get; } = [];

    public FakeAppRoleRepository Seed(params AppRole[] roles)
    {
        foreach (var role in roles)
        {
            _roles[role.RoleId] = role;
        }

        return this;
    }

    public Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default)
        => QueryAsync(new AppRoleQuery(), cancellationToken);

    public Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<AppRole> results = _roles.Values;

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            results = results.Where(r =>
                Contains(r.RoleId, keyword) ||
                Contains(r.RoleName, keyword) ||
                Contains(r.Description, keyword));
        }

        if (query.PermissionLevel is int level)
        {
            results = results.Where(r => r.PermissionLevel == level);
        }

        return Task.FromResult(results.OrderBy(r => r.RoleId, StringComparer.Ordinal).AsEnumerable());
    }

    public Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default)
        => Task.FromResult(_roles.GetValueOrDefault(roleId));

    public Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default)
        => Task.FromResult(_roles.ContainsKey(roleId));

    public Task<string> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        CreatedRoleIds.Add(request.RoleId);
        _roles[request.RoleId] = ToRole(request, _roles.Count + 1);
        return Task.FromResult(request.RoleId);
    }

    public Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (!_roles.TryGetValue(request.RoleId, out var existing))
        {
            return Task.FromResult(false);
        }

        UpdatedRoleIds.Add(request.RoleId);
        _roles[request.RoleId] = ToRole(request, existing.Pkid);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default)
    {
        DeletedRoleIds.Add(roleId);
        return Task.FromResult(_roles.Remove(roleId));
    }

    private static AppRole ToRole(AppRoleRequest request, int pkid) => new()
    {
        Pkid = pkid,
        RoleId = request.RoleId,
        RoleName = request.RoleName,
        PermissionLevel = request.PermissionLevel,
        Description = request.Description,
        UserIds = [.. request.UserIds],
        UserCount = request.UserIds.Count,
    };

    private static bool Contains(string? value, string keyword)
        => value is not null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
