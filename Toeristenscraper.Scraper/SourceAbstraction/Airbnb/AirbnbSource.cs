using Microsoft.Extensions.Options;
using System.Text.Json;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Parsing.Airbnb;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Airbnb.Operations;


namespace Toeristenscraper.Scraper.SourceAbstraction.Airbnb
{
    public class AirbnbSource(StaysSearch staysSearch, PdpAvailabilityCalendar pdpAvailabilityCalendar, IOptions<ScraperOptions> options, ILogger<AirbnbSource> logger) : IAccommodationSource
    {
        public string PlatformName { get; } = "Airbnb";
        public int PlatformId { get; } = 2;
        public int MaxDepth => options.Value.Airbnb.MaxDepth;

        public async Task<TileSearchResult> SearchTileAsync(Tile tile, CancellationToken ct)
        {
            using JsonDocument firstDoc = await staysSearch.GetPageAsync(tile, null, ct); // eerste pagina heeft cursor 'null' 
            var firstPage = StaysSearchParser.Parse(firstDoc);

            // limiet bereikt = tegel opsplitsen
            if (firstPage.PageCursors.Count >= StaysSearch.MaxPageCursors)
            {
                return new TileSearchResult(new List<DiscoveredAccommodation>(), LimitReached: true);
            }
            var allAccommodations = new List<DiscoveredAccommodation>();
            allAccommodations.AddRange(firstPage.Accommodations);
            
            for (int i = 1; i < firstPage.PageCursors.Count; i++)
            {
                using JsonDocument doc = await staysSearch.GetPageAsync(tile, firstPage.PageCursors[i], ct);
                StaysSearchPage page = StaysSearchParser.Parse(doc);
                allAccommodations.AddRange(page.Accommodations);
            }
            return new TileSearchResult(allAccommodations, LimitReached: false);

        }
        public async Task<IReadOnlyList<Availability>> GetAvailabilityAsync(DiscoveredAccommodation listing, CancellationToken ct)
        {
            // 12 kalendermaanden vanaf de huidige maand, in één request
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
            using JsonDocument doc = await pdpAvailabilityCalendar.GetPageAsync(listing.SourceId, today.Month, today.Year, ct);

            return PdpAvailabilityCalendarParser.Parse(doc, today).Calendars;
        }
    }
}
