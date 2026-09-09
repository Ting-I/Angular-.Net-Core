using System.Security.Claims;
using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// POST /api/courses/{id}/sheet — the 課程簡介 export record. The endpoint's entire output is one
/// structured log line, so that line is what these tests assert: its values, its shape, and the fact
/// that no row anywhere moved to produce it.
/// </summary>
public class CoursesControllerSheetTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 14, 30, 0, TimeSpan.Zero);

    /// <summary>A clock in UTC, so `GetLocalNow()` is the instant given and assertions are fixed.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static ClaimsPrincipal Principal(string userId, string userName) =>
        TestPrincipal.SignedIn(userId, userName).HttpContext!.User;

    private static (
        CoursesController Controller,
        FakeCourseRepository Courses,
        FakeAppUserRepository Users,
        CapturingLogger<CoursesController> Logger) CreateController(
        Course[] seed,
        ClaimsPrincipal? user = null,
        AppUser[]? users = null)
    {
        var courses = new FakeCourseRepository().Seed(seed);
        var appUsers = new FakeAppUserRepository().Seed(users ?? []);
        var logger = new CapturingLogger<CoursesController>();
        var controller = new CoursesController(courses, appUsers, logger, new FixedClock(Now))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

        return (controller, courses, appUsers, logger);
    }

    private static AppUser MakeUser(string userId, string userName) =>
        new() { Pkid = 1, UserId = userId, UserName = userName, IsActive = true };

    [Fact]
    public async Task LogSheetExport_ReturnsNoContent()
    {
        var (controller, _, _, _) = CreateController(
            [CoursesControllerTests.MakeCourse(1)],
            Principal("helen", "Helen Lin"));

        var result = await controller.LogSheetExport(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task LogSheetExport_ReturnsNotFoundForACourseThatDoesNotExist()
    {
        var (controller, _, _, logger) = CreateController(
            [CoursesControllerTests.MakeCourse(1)],
            Principal("helen", "Helen Lin"));

        var result = await controller.LogSheetExport(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        // Nothing to record: no course, no export.
        Assert.Empty(logger.Entries);
    }

    /// <summary>The whole point of the endpoint, and the whole point of it being an endpoint.</summary>
    [Fact]
    public async Task LogSheetExport_LogsTheOperatorTheCourseAndTheTime()
    {
        var (controller, _, _, logger) = CreateController(
            [CoursesControllerTests.MakeCourse(7, courseId: "AZ-104")],
            Principal("helen", "Helen Lin"),
            [MakeUser("helen", "Helen Lin")]);

        await controller.LogSheetExport(7, CancellationToken.None);

        var entry = logger.Single(LogLevel.Information);
        Assert.Equal("helen", entry.Text("UserId"));
        Assert.Equal("Helen Lin", entry.Text("UserName"));
        Assert.Equal(7, entry.Value("Pkid"));
        Assert.Equal("AZ-104", entry.Text("CourseId"));
        Assert.Equal(Now.ToString("O"), entry.Text("At"));
        Assert.Contains("print requested", entry.Message);
    }

    /// <summary>
    /// The reason the endpoint reads AppUser at all: a rename re-issues no token, so the claim in the
    /// operator's hand can be months old.
    /// </summary>
    [Fact]
    public async Task LogSheetExport_TakesTheUserNameFromAppUserNotFromTheToken()
    {
        var (controller, _, _, logger) = CreateController(
            [CoursesControllerTests.MakeCourse(1)],
            Principal("helen", "舊名"),
            [MakeUser("helen", "新名")]);

        await controller.LogSheetExport(1, CancellationToken.None);

        Assert.Equal("新名", logger.Single(LogLevel.Information).Text("UserName"));
    }

    [Fact]
    public async Task LogSheetExport_FallsBackToTheClaimWhenTheAppUserRowIsGone()
    {
        var (controller, _, _, logger) = CreateController(
            [CoursesControllerTests.MakeCourse(1)],
            Principal("helen", "Helen Lin"));

        await controller.LogSheetExport(1, CancellationToken.None);

        Assert.Equal("Helen Lin", logger.Single(LogLevel.Information).Text("UserName"));
    }

    [Fact]
    public async Task LogSheetExport_FallsBackToSystemWithNoUserIdClaim()
    {
        // Not reachable through the real pipeline — the fallback policy would have answered first —
        // but the line still has to say something rather than nothing.
        var (controller, _, _, logger) = CreateController([CoursesControllerTests.MakeCourse(1)]);

        await controller.LogSheetExport(1, CancellationToken.None);

        var entry = logger.Single(LogLevel.Information);
        Assert.Equal(RowAuditWriterDefaults.SystemUserName, entry.Text("UserId"));
        Assert.Equal(RowAuditWriterDefaults.SystemUserName, entry.Text("UserName"));
    }

    /// <summary>
    /// Both values are operator-editable, so a CR/LF in either would forge whole lines inside the one
    /// record meant as evidence of what was sent.
    /// </summary>
    [Fact]
    public async Task LogSheetExport_FlattensControlCharactersOutOfTheLoggedValues()
    {
        var (controller, _, _, logger) = CreateController(
            [CoursesControllerTests.MakeCourse(1, courseId: "AZ-104\r\nINFO: forged")],
            Principal("helen", "Helen\r\nLin"),
            [MakeUser("helen", "Helen\tLin\r\nINFO: forged")]);

        await controller.LogSheetExport(1, CancellationToken.None);

        var entry = logger.Single(LogLevel.Information);
        Assert.Equal("AZ-104  INFO: forged", entry.Text("CourseId"));
        Assert.Equal("Helen Lin  INFO: forged", entry.Text("UserName"));
        Assert.DoesNotContain("\n", entry.Text("CourseId"));
        Assert.DoesNotContain("\n", entry.Text("UserName"));
    }

    [Fact]
    public async Task LogSheetExport_TruncatesAValueLongerThanItsColumn()
    {
        var courseId = new string('A', 80);
        var (controller, _, _, logger) = CreateController(
            [CoursesControllerTests.MakeCourse(1, courseId: courseId)],
            Principal("helen", "Helen Lin"));

        await controller.LogSheetExport(1, CancellationToken.None);

        Assert.Equal(new string('A', 50), logger.Single(LogLevel.Information).Text("CourseId"));
    }

    /// <summary>
    /// The house rule binds Insert / Update / Delete, and this is none of them: no row is written, so
    /// there is no 異動紀錄 entry to write either. Proved rather than promised.
    /// </summary>
    [Fact]
    public async Task LogSheetExport_WritesNothingAtAll()
    {
        var course = CoursesControllerTests.MakeCourse(
            1,
            certificationPkids: [7],
            jobCategoryPkids: [3]);
        var (controller, courses, users, _) = CreateController(
            [course],
            Principal("helen", "Helen Lin"),
            [MakeUser("helen", "Helen Lin")]);

        await controller.LogSheetExport(1, CancellationToken.None);

        Assert.Empty(courses.CreatedPkids);
        Assert.Empty(courses.UpdatedPkids);
        Assert.Empty(courses.DeletedPkids);
        Assert.Equal([7], courses.CertificationLinks[1]);
        Assert.Equal<short[]>([3], [.. courses.JobCategoryLinks[1]]);
        Assert.Empty(users.UpdatedUserIds);
        Assert.Empty(users.UpdatedUserNames);
    }
}
