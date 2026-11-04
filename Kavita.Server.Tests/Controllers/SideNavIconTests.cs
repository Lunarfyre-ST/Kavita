using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database.Tests;
using Kavita.Models.DTOs.SideNav;
using Kavita.Models.Entities;
using Kavita.Models.Entities.User;
using Kavita.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Server.Tests.Controllers;

public class SideNavIconTests(ITestOutputHelper output) : AbstractDbTest(output)
{
    [Fact]
    public async Task IconPersistsAndOldClientsCanChangeVisibilityWithoutErasingIt()
    {
        var (uow, context, _) = await CreateDatabase();
        var user = new AppUser { UserName = "sidebar-owner" };
        context.AppUser.Add(user);
        await context.SaveChangesAsync();
        var stream = new AppUserSideNavStream { AppUserId = user.Id, Name = "Fantasy", StreamType = SideNavStreamType.SmartFilter, Visible = true };
        context.AppUserSideNavStream.Add(stream);
        await context.SaveChangesAsync();
        var service = new StreamService(uow, Substitute.For<IEventHub>(), Substitute.For<ILocalizationService>(), NullLogger<StreamService>.Instance);

        await service.UpdateSideNavStream(user.Id, new SideNavStreamDto { Id = stream.Id, Name = stream.Name, Visible = true, Icon = "⚔️" });
        Assert.Equal("⚔️", (await uow.UserRepository.GetSideNavStreams(user.Id)).Single().Icon);
        await service.UpdateSideNavStream(user.Id, new SideNavStreamDto { Id = stream.Id, Name = stream.Name, Visible = false });
        var hidden = (await uow.UserRepository.GetSideNavStreams(user.Id)).Single();
        Assert.False(hidden.Visible);
        Assert.Equal("⚔️", hidden.Icon);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateSideNavStream(user.Id + 1,
            new SideNavStreamDto { Id = stream.Id, Name = stream.Name, Visible = true, Icon = "🚀" }));
        await service.UpdateSideNavStream(user.Id, new SideNavStreamDto { Id = stream.Id, Name = stream.Name, Visible = true, Icon = "" });
        Assert.Null((await uow.UserRepository.GetSideNavStreams(user.Id)).Single().Icon);
    }
}
