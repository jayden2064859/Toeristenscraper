using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Analytics.Models
{
    // record model voor het resultaat van een berekend bezettingsgraad inzicht
    public record OccupancyResult(decimal? occupancy, int includedAccommodations, InsightMetadata metadata);
    
    
}

