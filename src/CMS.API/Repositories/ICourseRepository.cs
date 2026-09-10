using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseRepository
{
    Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default);

    /// <summary>Single course including both n-n pkid lists.</summary>
    Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>True when any course already uses this CourseId, optionally excluding one pkid.</summary>
    Task<bool> CourseIdExistsAsync(
        string courseId,
        int? excludePkid = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Duplicates a course under a new CourseId, carrying both n-n relations. Returns the new pkid,
    /// or null when the source course does not exist.
    /// </summary>
    Task<int?> CopyAsync(int pkid, string newCourseId, CancellationToken cancellationToken = default);
}
