using System;
using System.Collections.Generic;
using System.Text;
using Toeristenscraper.Data.Entities;

namespace Toeristenscraper.Analytics.Models
{
    // meest recente beschikbaarheidsobservatie van één accommodatie op één datum, basis voor alle analyses
    public record LatestObservation(int platformId, string sourceId, DateOnly availabilityDate, bool isAvailable, string? city, string? municipality, string? province, string? accommodationType);

}

