using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPartnerRepository
{
    Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default);

    Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);

    Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default);
}
