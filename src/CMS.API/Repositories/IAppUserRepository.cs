using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppUserRepository
{
    Task<IEnumerable<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the user. The hash is supplied by the caller — the repository never sees a
    /// plaintext password.
    /// </summary>
    Task<string> CreateAsync(AppUserRequest request, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>Updates the row and its role assignments. PasswordHash is never in the SET list.</summary>
    Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes UserName for one user and nothing else — not the key, and not the role assignments.
    /// What ProfileController calls for the signed-in operator's own account, where
    /// <see cref="UpdateAsync"/> would rewrite AppUserRole from a request that carries no roles.
    /// </summary>
    Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken cancellationToken = default);

    /// <summary>Writes PasswordHash and PasswordUpdatedTime, and nothing else.</summary>
    Task<bool> ResetPasswordAsync(string userId, string passwordHash, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default);
}
