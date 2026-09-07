using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseGroupRepository
{
    Task<IEnumerable<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken = default);

    Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);

    Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default);
}
