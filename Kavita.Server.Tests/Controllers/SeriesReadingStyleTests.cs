using Kavita.Models.Entities.Enums;
using Kavita.Services.Reading;

namespace Kavita.Server.Tests.Controllers;

public class SeriesReadingStyleTests
{
    [Fact]
    public void ManhwaUsesConfigurableSpeedWithoutChangingPageCount()
    {
        var standard = ReaderService.GetTimeEstimate(0, 600, false);
        var multiplier = ReaderService.GetImageReadingSpeedMultiplier(SeriesReadingStyle.Manhwa, 2f);
        var manhwa = ReaderService.GetTimeEstimate(0, 600, false, multiplier);

        Assert.Equal(standard.AvgHours / 2, manhwa.AvgHours, 5);
        Assert.Equal(600, manhwa.PageCount);
    }

    [Fact]
    public void OtherStylesAndEpubsKeepTheirExistingEstimates()
    {
        Assert.Equal(1f, ReaderService.GetImageReadingSpeedMultiplier(SeriesReadingStyle.Manga, 4f));
        Assert.Equal(1f, ReaderService.GetImageReadingSpeedMultiplier(SeriesReadingStyle.FollowProfile, 4f));
        Assert.Equal(ReaderService.GetTimeEstimate(20000, 0, true).AvgHours,
            ReaderService.GetTimeEstimate(20000, 0, true, 4f).AvgHours);
        Assert.Equal(1f, ReaderService.GetImageReadingSpeedMultiplier(SeriesReadingStyle.Manhwa, float.NaN));
    }
}
