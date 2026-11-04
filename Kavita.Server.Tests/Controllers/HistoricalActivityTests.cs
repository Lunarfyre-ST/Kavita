using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Store;
using Kavita.Common.Helpers;
using Kavita.Database.Extensions;
using Kavita.Database.Extensions.Filters;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Statistics;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.User;
using Kavita.Server.Controllers;
using Kavita.Services;
using Kavita.Services.Builders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Server.Tests.Controllers;

public class HistoricalActivityTests(ITestOutputHelper output) : AbstractDbTest(output)
{
    [Fact]
    public async Task ReclassificationIsReversibleAndPreservesProgressAndOtherActivity()
    {
        var (_, context, mapper) = await CreateDatabase();
        var library = await context.Library.Includes(LibraryIncludes.Series).FirstAsync();
        library.Series.Add(new SeriesBuilder("Example")
            .WithVolume(new VolumeBuilder("1")
                .WithChapter(new ChapterBuilder("1").WithPages(100).Build())
                .WithChapter(new ChapterBuilder("2").WithPages(20).Build()).Build()).Build());
        var owner = new AppUser { UserName = "owner" };
        var other = new AppUser { UserName = "other" };
        context.AppUser.AddRange(owner, other);
        await context.SaveChangesAsync();

        var start = new DateTime(2025, 4, 19, 12, 0, 0, DateTimeKind.Utc);
        var chapters = await context.Chapter.OrderBy(c => c.Id).ToListAsync();
        AppUserReadingSessionActivityData Activity(int chapterIndex, int pages, int startSeconds, int endSeconds) => new()
        {
            ChapterId = chapters[chapterIndex].Id, VolumeId = chapters[chapterIndex].VolumeId,
            SeriesId = library.Series.First().Id, LibraryId = library.Id, PagesRead = pages,
            StartPage = 0, EndPage = pages, TotalPages = pages, Format = MangaFormat.Archive,
            StartTimeUtc = start.AddSeconds(startSeconds), EndTimeUtc = start.AddSeconds(endSeconds)
        };
        var oldActivity = Activity(0, 100, 0, 60);
        var actualActivity = Activity(1, 20, 60, 80);
        var otherActivity = Activity(0, 100, 0, 60);
        context.AppUserReadingSession.AddRange(
            new AppUserReadingSession { AppUserId = owner.Id, ActivityData = [oldActivity, actualActivity], IsActive = false, StartTimeUtc = start, EndTimeUtc = start.AddMinutes(2) },
            new AppUserReadingSession { AppUserId = other.Id, ActivityData = [otherActivity], IsActive = false, StartTimeUtc = start, EndTimeUtc = start.AddMinutes(1) });
        context.AppUserProgresses.Add(new AppUserProgress { AppUserId = owner.Id, ChapterId = chapters[0].Id, VolumeId = chapters[0].VolumeId, SeriesId = library.Series.First().Id, LibraryId = library.Id, PagesRead = 100 });
        await context.SaveChangesAsync();

        var users = Substitute.For<IUserRepository>();
        users.GetUserByIdAsync(owner.Id, Arg.Any<AppUserIncludes>(), Arg.Any<CancellationToken>()).Returns(owner);
        users.GetSocialPreferencesForUser(owner.Id, Arg.Any<CancellationToken>()).Returns(new AppUserSocialPreferences());
        var uow = Substitute.For<IUnitOfWork>();
        uow.UserRepository.Returns(users);
        var localization = Substitute.For<ILocalizationService>();
        localization.TranslateAsync(owner.Id, Arg.Any<string>(), Arg.Any<object[]>()).Returns("label");
        var stats = new StatisticService(NullLogger<StatisticService>.Instance, context, mapper, uow,
            Substitute.For<IEntityNamingService>(), localization);
        var userContext = Substitute.For<IUserContext>();
        userContext.GetUserIdOrThrow().Returns(owner.Id);
        var services = new ServiceCollection().AddSingleton(userContext).BuildServiceProvider();
        var controller = new StatsController(stats, uow, context, Substitute.For<IDirectoryService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
        };
        var filter = new StatsFilterDto { Libraries = [library.Id], TimeZoneId = "UTC" };
        var dateKey = "2025-04-19";
        var before = (await stats.GetReadingActivityGraphData(filter, owner.Id, 2025, owner.Id))[dateKey];
        Assert.Equal(120, before.TotalPages);
        Assert.Equal(120, before.TotalTimeReadingSeconds);

        var unauthorized = await controller.UpdateHistoricalActivity(new UpdateHistoricalActivityDto { ActivityIds = [oldActivity.Id, otherActivity.Id], IsHistorical = true });
        Assert.IsType<NotFoundResult>(unauthorized);
        Assert.False(oldActivity.IsHistorical);

        Assert.IsType<OkResult>(await controller.UpdateHistoricalActivity(new UpdateHistoricalActivityDto { ActivityIds = [oldActivity.Id], IsHistorical = true }));
        var after = (await stats.GetReadingActivityGraphData(filter, owner.Id, 2025, owner.Id))[dateKey];
        Assert.Equal(20, after.TotalPages);
        Assert.Equal(20, after.TotalTimeReadingSeconds);
        Assert.False(actualActivity.IsHistorical);
        Assert.Equal(100, (await context.AppUserProgresses.SingleAsync()).PagesRead);
        var visibleInHistory = await context.AppUserReadingSessionActivityData
            .ApplyStatsFilter(filter, owner.Id, new AppUserSocialPreferences(), owner,
                onlyCompleted: false, includeHistorical: true).ToListAsync();
        Assert.Equal(2, visibleInHistory.Count);
        Assert.Single(visibleInHistory, a => a.IsHistorical);
        var history = await stats.GetReadingHistoryItems(filter, new UserParams { PageSize = 10 }, owner.Id, owner.Id);
        Assert.Equal(2, history.Count);
        Assert.Equal(oldActivity.Id, Assert.Single(Assert.Single(history, h => h.IsHistorical).Chapters).ActivityId);
        Assert.Equal(actualActivity.Id, Assert.Single(Assert.Single(history, h => !h.IsHistorical).Chapters).ActivityId);

        Assert.IsType<OkResult>(await controller.UpdateHistoricalActivity(new UpdateHistoricalActivityDto { ActivityIds = [oldActivity.Id], IsHistorical = false }));
        var restored = (await stats.GetReadingActivityGraphData(filter, owner.Id, 2025, owner.Id))[dateKey];
        Assert.Equal(120, restored.TotalPages);
    }
}
