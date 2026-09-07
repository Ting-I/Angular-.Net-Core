using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CourseRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Column list shared by INSERT and the copy INSERT ... SELECT. pkid is IDENTITY and is never
    /// written.
    /// </summary>
    private const string WritableColumns =
        "Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder, " +
        "Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, Hour, " +
        "ListPrice, LearningCredit, Material, Objective, Target, Prerequisites, Outline, " +
        "TowardCertOrExam, Note, OtherInfo, CanRepeat";

    public async Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default)
        => await QueryAsync(new CourseQuery(), cancellationToken);

    public async Task<IEnumerable<Course>> QueryAsync(
        CourseQuery query,
        CancellationToken cancellationToken = default)
    {
        var (where, parameters) = CourseSql.BuildWhere(query);
        var sql = $"{CourseSql.SelectBase}\n{where}\n{CourseSql.DefaultOrderBy}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await MultiMapAsync(connection, sql, new DynamicParameters(parameters), cancellationToken);
    }

    /// <summary>
    /// The record plus both n-n pkid lists. The two junction reads are separate queries on the same
    /// connection; the list endpoints deliberately leave both lists empty.
    /// </summary>
    public async Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var courses = await MultiMapAsync(
            connection,
            $"{CourseSql.SelectBase}\nWHERE c.pkid = @Pkid",
            new DynamicParameters(new { Pkid = pkid }),
            cancellationToken);

        var course = courses.SingleOrDefault();
        if (course is null)
        {
            return null;
        }

        course.CertificationPkids = (await connection.QueryAsync<int>(new CommandDefinition(
            CourseSql.SelectCertificationPkids,
            new { Pkid = pkid },
            cancellationToken: cancellationToken))).ToList();

        course.JobCategoryPkids = (await connection.QueryAsync<short>(new CommandDefinition(
            CourseSql.SelectJobCategoryPkids,
            new { Pkid = pkid },
            cancellationToken: cancellationToken))).ToList();

        return course;
    }

    /// <summary>
    /// pkid is int IDENTITY, so it is omitted from the INSERT and read back through
    /// SCOPE_IDENTITY(), which returns decimal and needs the explicit CAST. The INSERT and both
    /// junction back-fills share one transaction — a half-saved course is worse than a failed one.
    /// </summary>
    public async Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            INSERT INTO Course ({WritableColumns})
            VALUES (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
                @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff, @Hour,
                @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites, @Outline,
                @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ToScalarParameters(request),
            transaction,
            cancellationToken: cancellationToken));

        await SyncJunctionsAsync(connection, transaction, pkid, request, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    /// <summary>
    /// pkid is the primary key and is not updatable. The junctions are rewritten only when the row
    /// actually existed — there would be nothing to attach them to otherwise.
    /// </summary>
    public async Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE Course
            SET Title = @Title,
                OfficialTitle = @OfficialTitle,
                CourseId = @CourseId,
                ProdCourseId = @ProdCourseId,
                FriendlyUrl = @FriendlyUrl,
                DisplayOrder = @DisplayOrder,
                Partner_pkid = @PartnerPkid,
                CourseGroup_pkid = @CourseGroupPkid,
                PublishStatus_pkid = @PublishStatusPkid,
                ScheduleOn = @ScheduleOn,
                ScheduleOff = @ScheduleOff,
                Hour = @Hour,
                ListPrice = @ListPrice,
                LearningCredit = @LearningCredit,
                Material = @Material,
                Objective = @Objective,
                Target = @Target,
                Prerequisites = @Prerequisites,
                Outline = @Outline,
                TowardCertOrExam = @TowardCertOrExam,
                Note = @Note,
                OtherInfo = @OtherInfo,
                CanRepeat = @CanRepeat
            WHERE pkid = @Pkid;
            """,
            ToScalarParameters(request, includePkid: true),
            transaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        await SyncJunctionsAsync(connection, transaction, request.Pkid, request, cancellationToken);

        transaction.Commit();
        return true;
    }

    /// <summary>
    /// The junction rows are deleted explicitly rather than left to ON DELETE CASCADE, so the intent
    /// is visible here and does not change if the constraints are ever redeclared. The controller has
    /// already returned 409 if any child table still references the course.
    /// </summary>
    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await ClearJunctionsAsync(connection, transaction, pkid, cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Course WHERE pkid = @Pkid",
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return affected > 0;
    }

    public async Task<bool> CourseIdExistsAsync(
        string courseId,
        int? excludePkid = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1 FROM Course
                WHERE CourseId = @CourseId AND (@ExcludePkid IS NULL OR pkid <> @ExcludePkid)
            ) THEN 1 ELSE 0 END AS bit);
            """,
            new { CourseId = courseId, ExcludePkid = excludePkid },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// INSERT ... SELECT copies every scalar except pkid (fresh IDENTITY) and CourseId (the supplied
    /// value); both junctions are back-filled from the source rows. All of it in one transaction.
    /// SCOPE_IDENTITY() comes back NULL when the source pkid matched nothing, which surfaces as the
    /// null return the controller turns into a 404.
    /// </summary>
    public async Task<int?> CopyAsync(
        int pkid,
        string newCourseId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var newPkid = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            $"""
            INSERT INTO Course ({WritableColumns})
            SELECT c.Title, c.OfficialTitle, @NewCourseId, c.ProdCourseId, c.FriendlyUrl,
                   c.DisplayOrder, c.Partner_pkid, c.CourseGroup_pkid, c.PublishStatus_pkid,
                   c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice, c.LearningCredit,
                   c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline,
                   c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat
            FROM Course c
            WHERE c.pkid = @Pkid;
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new { Pkid = pkid, NewCourseId = newCourseId },
            transaction,
            cancellationToken: cancellationToken));

        if (newPkid is not int createdPkid)
        {
            transaction.Rollback();
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO CourseInCertification (Course_pkid, Certification_pkid)
            SELECT @NewPkid, Certification_pkid FROM CourseInCertification WHERE Course_pkid = @Pkid;
            """,
            new { Pkid = pkid, NewPkid = createdPkid },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO CourseJobCategories (Course_pkid, JobCategory_pkid)
            SELECT @NewPkid, JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @Pkid;
            """,
            new { Pkid = pkid, NewPkid = createdPkid },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return createdPkid;
    }

    /// <summary>
    /// Runs the projection through the Dapper multi-map. The LEFT JOIN to CourseGroup hands the
    /// lambda a constructed CourseGroupLookup even when every joined column is NULL, so the nav
    /// object is nulled out from the FK instead of being trusted — otherwise an ungrouped course
    /// reports pkid 0 with a blank description.
    /// </summary>
    private static async Task<List<Course>> MultiMapAsync(
        IDbConnection connection,
        string sql,
        DynamicParameters parameters,
        CancellationToken cancellationToken)
    {
        var courses = await connection
            .QueryAsync<Course, PartnerLookup, CourseGroupLookup, PublishStatusLookup, Course>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken),
                (course, partner, courseGroup, publishStatus) =>
                {
                    course.Partner = partner;
                    course.CourseGroup = course.CourseGroupPkid.HasValue ? courseGroup : null;
                    course.PublishStatus = publishStatus;
                    return course;
                },
                splitOn: CourseSql.SplitOn);

        return courses.ToList();
    }

    /// <summary>Delete-then-reinsert for both junctions, inside the caller transaction.</summary>
    private static async Task SyncJunctionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int pkid,
        CourseRequest request,
        CancellationToken cancellationToken)
    {
        await ClearJunctionsAsync(connection, transaction, pkid, cancellationToken);

        // Distinct() first: the composite PKs would reject a duplicate pair and take the whole
        // transaction down with it.
        var certificationPkids = request.CertificationPkids.Distinct().ToList();
        if (certificationPkids.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@Pkid, @RelatedPkid)",
                certificationPkids.Select(id => new { Pkid = pkid, RelatedPkid = id }),
                transaction,
                cancellationToken: cancellationToken));
        }

        var jobCategoryPkids = request.JobCategoryPkids.Distinct().ToList();
        if (jobCategoryPkids.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO CourseJobCategories (Course_pkid, JobCategory_pkid) VALUES (@Pkid, @RelatedPkid)",
                jobCategoryPkids.Select(id => new { Pkid = pkid, RelatedPkid = id }),
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ClearJunctionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int pkid,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid;
            DELETE FROM CourseJobCategories WHERE Course_pkid = @Pkid;
            """,
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// The scalar column parameters shared by INSERT and UPDATE. DateOnly values go through the
    /// registered DateOnlyTypeHandler, which binds them as DbType.Date.
    /// </summary>
    private static DynamicParameters ToScalarParameters(CourseRequest request, bool includePkid = false)
    {
        var parameters = new DynamicParameters();

        if (includePkid)
        {
            parameters.Add(nameof(request.Pkid), request.Pkid);
        }

        parameters.Add(nameof(request.Title), request.Title);
        parameters.Add(nameof(request.OfficialTitle), request.OfficialTitle);
        parameters.Add(nameof(request.CourseId), request.CourseId);
        parameters.Add(nameof(request.ProdCourseId), request.ProdCourseId);
        parameters.Add(nameof(request.FriendlyUrl), request.FriendlyUrl);
        parameters.Add(nameof(request.DisplayOrder), request.DisplayOrder);
        parameters.Add(nameof(request.PartnerPkid), request.PartnerPkid);
        parameters.Add(nameof(request.CourseGroupPkid), request.CourseGroupPkid);
        parameters.Add(nameof(request.PublishStatusPkid), request.PublishStatusPkid);
        parameters.Add(nameof(request.ScheduleOn), request.ScheduleOn);
        parameters.Add(nameof(request.ScheduleOff), request.ScheduleOff);
        parameters.Add(nameof(request.Hour), request.Hour);
        parameters.Add(nameof(request.ListPrice), request.ListPrice);
        parameters.Add(nameof(request.LearningCredit), request.LearningCredit);
        parameters.Add(nameof(request.Material), request.Material);
        parameters.Add(nameof(request.Objective), request.Objective);
        parameters.Add(nameof(request.Target), request.Target);
        parameters.Add(nameof(request.Prerequisites), request.Prerequisites);
        parameters.Add(nameof(request.Outline), request.Outline);
        parameters.Add(nameof(request.TowardCertOrExam), request.TowardCertOrExam);
        parameters.Add(nameof(request.Note), request.Note);
        parameters.Add(nameof(request.OtherInfo), request.OtherInfo);
        parameters.Add(nameof(request.CanRepeat), request.CanRepeat);

        return parameters;
    }
}
