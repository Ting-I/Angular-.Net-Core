using CMS.API.Controllers;
using CMS.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using static CMS.API.Tests.Controllers.CoursesControllerTests;

namespace CMS.API.Tests.Controllers;

/// <summary>Covers POST /api/courses/{id}/copy, the one non-standard endpoint on this controller.</summary>
public class CoursesControllerCopyTests
{
    private static CourseCopyRequest CopyRequest(string newCourseId = "AZ-104-COPY")
        => new() { NewCourseId = newCourseId };

    [Fact]
    public async Task Copy_Returns201WithTheNewKeyAndTheSuppliedCourseId()
    {
        var (controller, repository) = CreateController(MakeCourse(1, "AZ-104"));

        var result = await controller.Copy(1, CopyRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CoursesController.GetById), created.ActionName);

        var copy = Assert.IsType<Course>(created.Value);
        Assert.NotEqual(1, copy.Pkid);
        Assert.Equal("AZ-104-COPY", copy.CourseId);
        Assert.Equal(copy.Pkid, created.RouteValues!["id"]);
        Assert.Contains(copy.Pkid, repository.CreatedPkids);
    }

    [Fact]
    public async Task Copy_CarriesEveryOtherScalarAcross()
    {
        var source = MakeCourse(1, "AZ-104", "Azure 系統管理", displayOrder: 7, canRepeat: true);
        var (controller, _) = CreateController(source);

        var result = await controller.Copy(1, CopyRequest(), CancellationToken.None);
        var copy = Assert.IsType<Course>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);

        Assert.Equal(source.Title, copy.Title);
        Assert.Equal(source.ProdCourseId, copy.ProdCourseId);
        Assert.Equal(source.FriendlyUrl, copy.FriendlyUrl);
        Assert.Equal(source.DisplayOrder, copy.DisplayOrder);
        Assert.Equal(source.PartnerPkid, copy.PartnerPkid);
        Assert.Equal(source.CourseGroupPkid, copy.CourseGroupPkid);
        Assert.Equal(source.PublishStatusPkid, copy.PublishStatusPkid);
        Assert.Equal(source.ScheduleOn, copy.ScheduleOn);
        Assert.Equal(source.ScheduleOff, copy.ScheduleOff);
        Assert.Equal(source.Hour, copy.Hour);
        Assert.Equal(source.ListPrice, copy.ListPrice);
        Assert.Equal(source.LearningCredit, copy.LearningCredit);
        Assert.True(copy.CanRepeat);
    }

    [Fact]
    public async Task Copy_CarriesBothNnRelations()
    {
        var (controller, repository) = CreateController(
            MakeCourse(1, certificationPkids: [7, 9], jobCategoryPkids: [3]));

        var result = await controller.Copy(1, CopyRequest(), CancellationToken.None);
        var copy = Assert.IsType<Course>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);

        Assert.Equal([7, 9], copy.CertificationPkids);
        Assert.Equal<short[]>([3], [.. copy.JobCategoryPkids]);
        // The source keeps its own rows.
        Assert.Equal([7, 9], repository.CertificationLinks[1]);
    }

    [Fact]
    public async Task Copy_WhenTheSourceIsMissing_Returns404()
    {
        var (controller, repository) = CreateController(MakeCourse(1));

        var result = await controller.Copy(99, CopyRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Empty(repository.CreatedPkids);
    }

    [Fact]
    public async Task Copy_WhenTheNewCourseIdIsTaken_Returns409()
    {
        var (controller, repository) = CreateController(
            MakeCourse(1, "AZ-104"),
            MakeCourse(2, "AZ-204"));

        var result = await controller.Copy(1, CopyRequest("AZ-204"), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("簡介代碼已存在", problem.Title);
        Assert.Empty(repository.CreatedPkids);
    }

    [Fact]
    public async Task Copy_TrimsTheSuppliedCourseId()
    {
        var (controller, _) = CreateController(MakeCourse(1, "AZ-104"));

        var result = await controller.Copy(1, CopyRequest("  AZ-104-COPY  "), CancellationToken.None);
        var copy = Assert.IsType<Course>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);

        Assert.Equal("AZ-104-COPY", copy.CourseId);
    }

    /// <summary>The duplicate check runs against the trimmed value, not the raw input.</summary>
    [Fact]
    public async Task Copy_DetectsADuplicateAfterTrimming()
    {
        var (controller, _) = CreateController(MakeCourse(1, "AZ-104"), MakeCourse(2, "AZ-204"));

        var result = await controller.Copy(1, CopyRequest("  AZ-204  "), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
