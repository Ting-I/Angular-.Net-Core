using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppRoleRepository
{
    Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default);

    Task<AppRole?> GetByIdAsync(string roleId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string roleId, CancellationToken cancellationToken = default);

    Task<string> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string roleId, CancellationToken cancellationToken = default);
}
