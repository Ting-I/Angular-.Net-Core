using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPublishStatusRepository
{
    Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default);

    Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(byte pkid, CancellationToken cancellationToken = default);

    Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default);
}
