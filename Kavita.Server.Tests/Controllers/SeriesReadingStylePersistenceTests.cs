using EasyCaching.Core;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.API.Store;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.User;
using Kavita.Server.Controllers;
using Kavita.Services.Builders;
using Kavita.Services.Reading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Server.Tests.Controllers;

public class SeriesReadingStylePersistenceTests(ITestOutputHelper output) : AbstractDbTest(output)
{
    [Fact]
    public async Task MixedReadingListUsesEachSeriesSpeedWithoutChangingPageCount()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var mangaChapter = new ChapterBuilder("1").WithPages(100).Build();
        var manhwaChapter = new ChapterBuilder("1").WithPages(200).Build();
        var manga = new SeriesBuilder("Manga").WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1").WithChapter(mangaChapter).Build()).Build();
        var manhwa = new SeriesBuilder("Manhwa").WithFormat(MangaFormat.Archive)
            .WithVolume(new VolumeBuilder("1").WithChapter(manhwaChapter).Build()).Build();
        manhwa.ReadingStyle = SeriesReadingStyle.Manhwa;
        manhwa.ManhwaSpeedMultiplier = 2f;
        context.Library.Add(new LibraryBuilder("Mixed").WithSeries(manga).WithSeries(manhwa).Build());
        var owner = new AppUser { UserName = "owner" };
        context.AppUser.Add(owner);
        await context.SaveChangesAsync();
        var list = new ReadingList
        {
            Title = "Mixed list", NormalizedTitle = "MIXED LIST", AppUserId = owner.Id, AgeRating = AgeRating.Unknown,
            Items =
            [
                new ReadingListItem { SeriesId = manga.Id, VolumeId = mangaChapter.VolumeId, ChapterId = mangaChapter.Id },
                new ReadingListItem { SeriesId = manhwa.Id, VolumeId = manhwaChapter.VolumeId, ChapterId = manhwaChapter.Id }
            ]
        };
        context.ReadingList.Add(list);
        await context.SaveChangesAsync();

        var info = await unitOfWork.ReadingListRepository.GetReadingListInfoAsync(list.Id);
        Assert.NotNull(info);
        Assert.Equal(300, info.Pages);
        Assert.Equal(200, info.EstimatedImagePages);
        var estimate = ReaderService.GetTimeEstimate(info.WordCount, info.Pages, info.IsAllEpub, estimatedImagePages: info.EstimatedImagePages);
        Assert.Equal(ReaderService.GetTimeEstimate(0, 200, false).AvgHours, estimate.AvgHours);
        Assert.Equal(300, estimate.PageCount);
    }

    [Fact]
    public async Task SavingManhwaStylePersistsItAndSchedulesEstimateRefresh()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        var series = new SeriesBuilder("Example").WithFormat(MangaFormat.Archive).Build();
        context.Library.Add(new LibraryBuilder("Style test").WithSeries(series).Build());
        var owner = new AppUser { UserName = "owner" };
        context.AppUser.Add(owner);
        await context.SaveChangesAsync();

        var userContext = Substitute.For<IUserContext>();
        userContext.GetUserIdOrThrow().Returns(owner.Id);
        var services = new ServiceCollection().AddSingleton(userContext).BuildServiceProvider();
        var scheduler = Substitute.For<ITaskScheduler>();
        var controller = new SeriesController(NullLogger<SeriesController>.Instance, scheduler, unitOfWork,
            Substitute.For<ISeriesService>(), Substitute.For<IEasyCachingProviderFactory>(),
            Substitute.For<ILocalizationService>(), Substitute.For<IExternalMetadataService>(), Substitute.For<IHostEnvironment>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
        };

        var response = await controller.UpdateSeries(new UpdateSeriesDto
        {
            Id = series.Id, Name = series.Name, SortName = series.SortName, LocalizedName = series.LocalizedName,
            ReadingStyle = SeriesReadingStyle.Manhwa, ManhwaSpeedMultiplier = 3f
        });
        var dto = Assert.IsType<SeriesDto>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(SeriesReadingStyle.Manhwa, dto.ReadingStyle);
        Assert.Equal(3f, dto.ManhwaSpeedMultiplier);
        scheduler.Received(1).AnalyzeFilesForSeries(series.LibraryId, series.Id);

        var legacyUpdate = await controller.UpdateSeries(new UpdateSeriesDto
        {
            Id = series.Id, Name = series.Name, SortName = "Example sort", LocalizedName = series.LocalizedName
        });
        var legacyDto = Assert.IsType<SeriesDto>(Assert.IsType<OkObjectResult>(legacyUpdate.Result).Value);
        Assert.Equal(SeriesReadingStyle.Manhwa, legacyDto.ReadingStyle);
        Assert.Equal(3f, legacyDto.ManhwaSpeedMultiplier);

        var invalidUpdate = await controller.UpdateSeries(new UpdateSeriesDto
        {
            Id = series.Id, ReadingStyle = SeriesReadingStyle.Manhwa, ManhwaSpeedMultiplier = 0f
        });
        Assert.IsType<BadRequestObjectResult>(invalidUpdate.Result);
        Assert.Equal(3f, series.ManhwaSpeedMultiplier);
    }
}
