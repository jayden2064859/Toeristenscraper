using System;
using System.Collections.Generic;
using System.Text;
using Toeristenscraper.Data.Entities;

namespace Toeristenscraper.Analytics.Models
{
    // meest recente beschikbaarheidsobservatie van één accommodatie op één datum, basis voor alle analyses
    // properties met init (geen record), zodat EF Core er na de projectie nog op kan filteren en groeperen
    public record LatestObservation
    {
        public int platformId { get; init; }
        public string sourceId { get; init; } = string.Empty;
        public DateOnly availabilityDate { get; init; }
        public bool isAvailable { get; init; }
        public string? city { get; init; }
        public string? municipality { get; init; }
        public string? province { get; init; }
        public string? accommodationType { get; init; }
        public DateOnly scrapingDate { get; init; }
    }
}

