using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>
/// Reads what a login attempt needs. Not an entity CRUD repository — AppUser CRUD lives in
/// <see cref="IAppUserRepository"/>, which never selects PasswordHash.
/// </summary>
public interface IAuthRepository
{
    /// <summary>
    /// The credential row and role assignments for a UserId, or null when no such user exists.
    /// The row is returned whatever IsActive says: the caller decides, so that an inactive account
    /// and a wrong password produce the same answer.
    /// </summary>
    Task<AppUserCredential?> GetCredentialAsync(string userId, CancellationToken cancellationToken = default);
}
