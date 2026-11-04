using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.API.Store;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.Entities.User;
using Kavita.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Kavita.Server.Tests.Controllers;

public class FilterControllerTests
{
    [Fact]
    public async Task CreateSeriesFilterDoesNotReplaceAnExistingFilter()
    {
        var existing = new AppUserSmartFilter { Name = "Re:Zero", Filter = "original", EntityType = FilterEntityType.Series };
        var user = new AppUser { SmartFilters = [existing] };
        var (controller, unitOfWork) = CreateController(user);

        var result = await controller.CreateSeriesSmartFilter(new SeriesFilterV2Dto { Name = "re:zero" });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("original", existing.Filter);
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSeriesFilterAddsANewFilter()
    {
        var user = new AppUser { SmartFilters = [] };
        var (controller, unitOfWork) = CreateController(user);

        var result = await controller.CreateSeriesSmartFilter(new SeriesFilterV2Dto { Name = "Re:Zero" });

        Assert.IsType<OkResult>(result);
        Assert.Equal("Re:Zero", Assert.Single(user.SmartFilters).Name);
        await unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    private static (FilterController, IUnitOfWork) CreateController(AppUser user)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var users = Substitute.For<IUserRepository>();
        unitOfWork.UserRepository.Returns(users);
        unitOfWork.HasChanges().Returns(true);
        users.GetUserByIdAsync(1, AppUserIncludes.SmartFilters, Arg.Any<CancellationToken>()).Returns(user);

        var userContext = Substitute.For<IUserContext>();
        userContext.GetUserIdOrThrow().Returns(1);
        var services = new ServiceCollection().AddSingleton(userContext).BuildServiceProvider();
        var controller = new FilterController(unitOfWork, Substitute.For<ILocalizationService>(),
            Substitute.For<IStreamService>(), NullLogger<FilterController>.Instance, Substitute.For<IEventHub>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
        };
        return (controller, unitOfWork);
    }
}
