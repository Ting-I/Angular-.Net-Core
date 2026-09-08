using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAppUserRepository"/> that mirrors the real repository's contract
/// (string PK, delete-then-reinsert n-n, RoleCount projection, server-written PasswordHash) so
/// controller behaviour can be tested without SQL Server.
///
/// The stored hashes are exposed so tests can assert what the create and reset paths wrote — and,
/// just as importantly, that the update path wrote nothing.
/// </summary>
public class FakeAppUserRepository : IAppUserRepository
{
    private readonly Dictionary<string, AppUser> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _passwordHashes = new(StringComparer.OrdinalIgnoreCase);

    public List<string> CreatedUserIds { get; } = [];
    public List<string> UpdatedUserIds { get; } = [];
    public List<string> ResetUserIds { get; } = [];

    /// <summary>Every (UserId, UserName) pair the profile endpoint wrote, in order.</summary>
    public List<(string UserId, string UserName)> UpdatedUserNames { get; } = [];

    /// <summary>Clock stand-in for PasswordUpdatedTime, so assertions are not time-dependent.</summary>
    public DateTime UtcNow { get; set; } = new(2026, 9, 8, 3, 0, 0, DateTimeKind.Utc);

    public FakeAppUserRepository Seed(params AppUser[] users)
    {
        foreach (var user in users)
        {
            _users[user.UserId] = user;
            _passwordHashes[user.UserId] = $"seed-hash-{user.UserId}";
        }

        return this;
    }

    /// <summary>The stored hash, or null when the user does not exist.</summary>
    public string? PasswordHashOf(string userId) => _passwordHashes.GetValueOrDefault(userId);

    public Task<IEnumerable<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
        => QueryAsync(new AppUserQuery(), cancellationToken);

    public Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<AppUser> results = _users.Values;

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            results = results.Where(u => Contains(u.UserId, keyword) || Contains(u.UserName, keyword));
        }

        // Tri-state: false is a filter in its own right.
        if (query.IsActive is bool isActive)
        {
            results = results.Where(u => u.IsActive == isActive);
        }

        var roleId = query.RoleId?.Trim();
        if (!string.IsNullOrEmpty(roleId))
        {
            results = results.Where(u => u.RoleIds.Contains(roleId, StringComparer.OrdinalIgnoreCase));
        }

        // A null PasswordUpdatedTime drops out as soon as either bound is set.
        if (query.PasswordUpdatedFrom is DateOnly from)
        {
            var lower = from.ToDateTime(TimeOnly.MinValue);
            results = results.Where(u => u.PasswordUpdatedTime is DateTime t && t >= lower);
        }

        if (query.PasswordUpdatedTo is DateOnly to)
        {
            var upper = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            results = results.Where(u => u.PasswordUpdatedTime is DateTime t && t < upper);
        }

        return Task.FromResult(results.OrderBy(u => u.UserId, StringComparer.Ordinal).AsEnumerable());
    }

    public Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.GetValueOrDefault(userId));

    public Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.ContainsKey(userId));

    public Task<string> CreateAsync(
        AppUserRequest request,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        CreatedUserIds.Add(request.UserId);
        _users[request.UserId] = ToUser(request, _users.Count + 1, UtcNow);
        _passwordHashes[request.UserId] = passwordHash;
        return Task.FromResult(request.UserId);
    }

    public Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(request.UserId, out var existing))
        {
            return Task.FromResult(false);
        }

        UpdatedUserIds.Add(request.UserId);
        // PasswordHash and PasswordUpdatedTime carry over untouched — that is the rule under test.
        _users[request.UserId] = ToUser(request, existing.Pkid, existing.PasswordUpdatedTime);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Writes UserName alone. Roles, IsActive, the key and the password hash all carry over
    /// untouched — the promise ProfileController leans on.
    /// </summary>
    public Task<bool> UpdateUserNameAsync(
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(userId, out var existing))
        {
            return Task.FromResult(false);
        }

        UpdatedUserNames.Add((userId, userName));
        existing.UserName = userName;
        return Task.FromResult(true);
    }

    public Task<bool> ResetPasswordAsync(
        string userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(userId, out var existing))
        {
            return Task.FromResult(false);
        }

        ResetUserIds.Add(userId);
        _passwordHashes[userId] = passwordHash;
        existing.PasswordUpdatedTime = UtcNow;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        _passwordHashes.Remove(userId);
        return Task.FromResult(_users.Remove(userId));
    }

    private static AppUser ToUser(AppUserRequest request, int pkid, DateTime? passwordUpdatedTime)
    {
        var roleIds = request.RoleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AppUser
        {
            Pkid = pkid,
            UserId = request.UserId,
            UserName = request.UserName,
            IsActive = request.IsActive,
            PasswordUpdatedTime = passwordUpdatedTime,
            RoleIds = roleIds,
            RoleCount = roleIds.Count,
        };
    }

    private static bool Contains(string? value, string keyword)
        => value is not null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
