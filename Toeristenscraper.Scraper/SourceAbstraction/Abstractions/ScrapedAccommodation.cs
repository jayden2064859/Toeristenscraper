using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // resultaat van discovery + alle detailoperaties (voor 1 listing)
    public class ScrapedAccommodation
    {
        public required DiscoveredAccommodation Accommodation { get; init; }
        public required IReadOnlyList<Availability> Availability { get; init; }

        // later komen hier de overige detailoperaties bij (kamers, voorzieningen, kvk/registratienummers etc) 
    }
}
