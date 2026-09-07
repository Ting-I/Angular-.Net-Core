using CMS.API.Controllers;
using CMS.API.Models;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Controllers;

public class LookupsControllerTests
{
    [Fact]
    public async Task GetAppUsers_ReturnsLookupList()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            AppUsers =
            [
                new AppUserLookup { UserId = "helen", UserName = "helen", IsActive = true },
                new AppUserLookup { UserId = "miles", UserName = "Miles Sun", IsActive = true },
            ],
        });

        var result = await controller.GetAppUsers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsAssignableFrom<IEnumerable<AppUserLookup>>(ok.Value).ToList();
        Assert.Equal(2, users.Count);
        Assert.Equal("helen", users[0].UserId);
    }

    [Fact]
    public async Task GetPublishStatuses_ReturnsLookupList()
    {
        var controller = new LookupsController(new FakeLookupRepository
        {
            PublishStatuses =
            [
                new PublishStatusLookup { Pkid = 1, Description = "草稿" },
                new PublishStatusLookup { Pkid = 2, Description = "已發布" },
            ],
        });

        var result = await controller.GetPublishStatuses(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var statuses = Assert.IsAssignableFrom<IEnumerable<PublishStatusLookup>>(ok.Value).ToList();
        Assert.Equal(2, statuses.Count);
        Assert.Equal(1, statuses[0].Pkid);
        Assert.Equal("已發布", statuses[1].Description);
    }
}
