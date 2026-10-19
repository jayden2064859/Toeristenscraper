using System;
using System.Collections.Generic;
using System.Text;
using Toeristenscraper.Scraper.Parsing.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.SourceAbstraction.Booking
{
    // implementeert IAccommodationSource: eerst pagina 1, bij >1000 resultaten meteen “limiet bereikt”, anders de overige pagina’s ophalen via offset
    public class BookingSource(FullSearch fullSearch) : IAccommodationSource
    {
        public int PlatformId { get; } = 1; // booking.com = 1

        public async Task<TileSearchResult> SearchTileAsync(Tile tile, CancellationToken ct)
        {

            using var firstDoc = await fullSearch.GetPageAsync(tile, 0, ct);
            FullSearchPage first = FullSearchParser.Parse(firstDoc);

            if (first.NbResultsTotal > FullSearch.MaxResults)
                return new TileSearchResult([], LimitReached: true);


            var all = new List<DiscoveredAccommodation>(first.Accommodations);
            for (int offset = FullSearch.PageSize; offset < first.NbResultsTotal; offset += FullSearch.PageSize)
            {
                using var doc = await fullSearch.GetPageAsync(tile, offset, ct);
                all.AddRange(FullSearchParser.Parse(doc).Accommodations);
            }

            return new TileSearchResult(all, LimitReached: false);
        }
    }
}
