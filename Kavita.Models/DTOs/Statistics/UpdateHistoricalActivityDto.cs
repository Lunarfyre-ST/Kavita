using System.Collections.Generic;

namespace Kavita.Models.DTOs.Statistics;

public sealed record UpdateHistoricalActivityDto
{
    public List<int> ActivityIds { get; init; } = [];
    public bool IsHistorical { get; init; }
}
