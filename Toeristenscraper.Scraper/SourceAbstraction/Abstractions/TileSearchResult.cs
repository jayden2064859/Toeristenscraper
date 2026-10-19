using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // uitkomst van het doorzoeken van één tegel.
    //  de tegel moet worden opgesplitst wanneer LimitReached = true
    public record TileSearchResult(IReadOnlyList<DiscoveredAccommodation> Accommodations,bool LimitReached);
}