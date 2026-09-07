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
}
