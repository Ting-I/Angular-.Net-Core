using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="ICourseRepository"/> that mirrors the real repository contract: int
/// IDENTITY key assignment, keyword search across the five short columns, exact FK matching,
/// inclusive date ranges, tri-state CanRepeat, DisplayOrder-then-pkid ordering, n-n lists returned
/// only by <see cref="GetByIdAsync"/>, and settable child counts so the delete-409 path is
/// reachable — all without SQL Server.
/// </summary>
public class FakeCourseRepository : ICourseRepository
{
    private readonly Dictionary<int, Course> _courses = [];
    private int _nextPkid = 1;

    public List<int> CreatedPkids { get; } = [];
    public List<int> UpdatedPkids { get; } = [];
    public List<int> DeletedPkids { get; } = [];

    /// <summary>Junction contents, keyed by course pkid — the two tables the repository rewrites.</summary>
    public Dictionary<int, List<int>> CertificationLinks { get; } = [];
    public Dictionary<int, List<short>> JobCategoryLinks { get; } = [];

    public FakeCourseRepository Seed(params Course[] courses)
    {
        foreach (var course in courses)
        {
            _courses[course.Pkid] = course;
            CertificationLinks[course.Pkid] = [.. course.CertificationPkids];
            JobCategoryLinks[course.Pkid] = [.. course.JobCategoryPkids];

            if (course.Pkid >= _nextPkid)
            {
                _nextPkid = course.Pkid + 1;
            }
        }

        return this;
    }

    public Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default)
        => QueryAsync(new CourseQuery(), cancellationToken);

    public Task<IEnumerable<Course>> QueryAsync(
        CourseQuery query,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Course> results = _courses.Values;

        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            results = results.Where(c =>
                Contains(c.Title, keyword)
                || Contains(c.OfficialTitle, keyword)
                || Contains(c.CourseId, keyword)
                || Contains(c.ProdCourseId, keyword)
                || Contains(c.FriendlyUrl, keyword));
        }

        if (query.PartnerPkid is short partnerPkid)
        {
            results = results.Where(c => c.PartnerPkid == partnerPkid);
        }

        if (query.CourseGroupPkid is short courseGroupPkid)
        {
            results = results.Where(c => c.CourseGroupPkid == courseGroupPkid);
        }

        if (query.PublishStatusPkid is byte publishStatusPkid)
        {
            results = results.Where(c => c.PublishStatusPkid == publishStatusPkid);
        }

        // Both bounds inclusive, and each applies on its own so an open-ended range works.
        if (query.ScheduleOnFrom is DateOnly scheduleOnFrom)
        {
            results = results.Where(c => c.ScheduleOn >= scheduleOnFrom);
        }

        if (query.ScheduleOnTo is DateOnly scheduleOnTo)
        {
            results = results.Where(c => c.ScheduleOn <= scheduleOnTo);
        }

        if (query.ScheduleOffFrom is DateOnly scheduleOffFrom)
        {
            results = results.Where(c => c.ScheduleOff >= scheduleOffFrom);
        }

        if (query.ScheduleOffTo is DateOnly scheduleOffTo)
        {
            results = results.Where(c => c.ScheduleOff <= scheduleOffTo);
        }

        if (query.CanRepeat is bool canRepeat)
        {
            results = results.Where(c => c.CanRepeat == canRepeat);
        }

        // The list endpoints never carry the n-n lists — GetById is the only place they load.
        var ordered = results
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Pkid)
            .Select(WithoutJunctions)
            .ToList();

        return Task.FromResult(ordered.AsEnumerable());
    }

    public Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        if (!_courses.TryGetValue(pkid, out var course))
        {
            return Task.FromResult<Course?>(null);
        }

        var withJunctions = Clone(course);
        withJunctions.CertificationPkids = [.. Links(CertificationLinks, pkid).Order()];
        withJunctions.JobCategoryPkids = [.. Links(JobCategoryLinks, pkid).Order()];

        return Task.FromResult<Course?>(withJunctions);
    }

    /// <summary>The key is server-generated, exactly as the IDENTITY column is.</summary>
    public Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        var pkid = _nextPkid++;
        CreatedPkids.Add(pkid);

        _courses[pkid] = FromRequest(request, pkid);
        WriteJunctions(pkid, request);

        return Task.FromResult(pkid);
    }

    public Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        if (!_courses.TryGetValue(request.Pkid, out var existing))
        {
            return Task.FromResult(false);
        }

        UpdatedPkids.Add(request.Pkid);

        var updated = FromRequest(request, request.Pkid);
        // Reference counts are projected from other tables, so an update never changes them.
        updated.CourseFaqCount = existing.CourseFaqCount;
        updated.CourseRelatedLinkCount = existing.CourseRelatedLinkCount;
        updated.HotCourseCount = existing.HotCourseCount;
        updated.CourseRecommCount = existing.CourseRecommCount;
        updated.Partner = existing.Partner;
        updated.CourseGroup = existing.CourseGroup;
        updated.PublishStatus = existing.PublishStatus;

        _courses[request.Pkid] = updated;
        WriteJunctions(request.Pkid, request);

        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var removed = _courses.Remove(pkid);
        if (removed)
        {
            DeletedPkids.Add(pkid);
            // The junction rows go with the course, as the real DELETE does inside its transaction.
            CertificationLinks.Remove(pkid);
            JobCategoryLinks.Remove(pkid);
        }

        return Task.FromResult(removed);
    }

    public Task<bool> CourseIdExistsAsync(
        string courseId,
        int? excludePkid = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_courses.Values.Any(c =>
            c.CourseId == courseId && (excludePkid is null || c.Pkid != excludePkid)));

    public Task<int?> CopyAsync(
        int pkid,
        string newCourseId,
        CancellationToken cancellationToken = default)
    {
        if (!_courses.TryGetValue(pkid, out var source))
        {
            return Task.FromResult<int?>(null);
        }

        var newPkid = _nextPkid++;
        CreatedPkids.Add(newPkid);

        var copy = Clone(source);
        copy.Pkid = newPkid;
        copy.CourseId = newCourseId;
        _courses[newPkid] = copy;

        CertificationLinks[newPkid] = [.. Links(CertificationLinks, pkid)];
        JobCategoryLinks[newPkid] = [.. Links(JobCategoryLinks, pkid)];

        return Task.FromResult<int?>(newPkid);
    }

    private static bool Contains(string? value, string keyword)
        => value is not null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static List<T> Links<T>(Dictionary<int, List<T>> links, int pkid)
        => links.TryGetValue(pkid, out var values) ? values : [];

    /// <summary>Delete-then-reinsert, distinct — the same shape the real junction sync has.</summary>
    private void WriteJunctions(int pkid, CourseRequest request)
    {
        CertificationLinks[pkid] = [.. request.CertificationPkids.Distinct()];
        JobCategoryLinks[pkid] = [.. request.JobCategoryPkids.Distinct()];
    }

    private static Course WithoutJunctions(Course course)
    {
        var copy = Clone(course);
        copy.CertificationPkids = [];
        copy.JobCategoryPkids = [];
        return copy;
    }

    private static Course Clone(Course course) => new()
    {
        Pkid = course.Pkid,
        Title = course.Title,
        OfficialTitle = course.OfficialTitle,
        CourseId = course.CourseId,
        ProdCourseId = course.ProdCourseId,
        FriendlyUrl = course.FriendlyUrl,
        DisplayOrder = course.DisplayOrder,
        PartnerPkid = course.PartnerPkid,
        CourseGroupPkid = course.CourseGroupPkid,
        PublishStatusPkid = course.PublishStatusPkid,
        ScheduleOn = course.ScheduleOn,
        ScheduleOff = course.ScheduleOff,
        Hour = course.Hour,
        ListPrice = course.ListPrice,
        LearningCredit = course.LearningCredit,
        Material = course.Material,
        Objective = course.Objective,
        Target = course.Target,
        Prerequisites = course.Prerequisites,
        Outline = course.Outline,
        TowardCertOrExam = course.TowardCertOrExam,
        Note = course.Note,
        OtherInfo = course.OtherInfo,
        CanRepeat = course.CanRepeat,
        Partner = course.Partner,
        CourseGroup = course.CourseGroup,
        PublishStatus = course.PublishStatus,
        CourseFaqCount = course.CourseFaqCount,
        CourseRelatedLinkCount = course.CourseRelatedLinkCount,
        HotCourseCount = course.HotCourseCount,
        CourseRecommCount = course.CourseRecommCount,
        CertificationPkids = [.. course.CertificationPkids],
        JobCategoryPkids = [.. course.JobCategoryPkids],
    };

    private static Course FromRequest(CourseRequest request, int pkid) => new()
    {
        Pkid = pkid,
        Title = request.Title,
        OfficialTitle = request.OfficialTitle,
        CourseId = request.CourseId,
        ProdCourseId = request.ProdCourseId,
        FriendlyUrl = request.FriendlyUrl,
        DisplayOrder = request.DisplayOrder,
        PartnerPkid = request.PartnerPkid,
        CourseGroupPkid = request.CourseGroupPkid,
        PublishStatusPkid = request.PublishStatusPkid,
        ScheduleOn = request.ScheduleOn,
        ScheduleOff = request.ScheduleOff,
        Hour = request.Hour,
        ListPrice = request.ListPrice,
        LearningCredit = request.LearningCredit,
        Material = request.Material,
        Objective = request.Objective,
        Target = request.Target,
        Prerequisites = request.Prerequisites,
        Outline = request.Outline,
        TowardCertOrExam = request.TowardCertOrExam,
        Note = request.Note,
        OtherInfo = request.OtherInfo,
        CanRepeat = request.CanRepeat,
    };
}
