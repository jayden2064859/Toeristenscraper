using Microsoft.Extensions.Options;
using System.Text.Json;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Parsing.Airbnb;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Airbnb.Operations;


namespace Toeristenscraper.Scraper.SourceAbstraction.Airbnb
{
    public class AirbnbSource(StaysSearch staysSearch, PdpAvailabilityCalendar pdpAvailabilityCalendar, StaysPdpSections staysPdpSections, 
                                IOptions<ScraperOptions> options, ILogger<AirbnbSource> logger) : IAccommodationSource
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

        public async Task<AccommodationDetails> GetDetailsAsync(DiscoveredAccommodation listing, IReadOnlyList<Availability> availability, CancellationToken ct)
        {
            // search parameter opties uit appsettings worden gebruikt
            SearchParameterOptions search = options.Value.SearchParameters;
            DateOnly checkIn = DateOnly.FromDateTime(DateTime.Today).AddDays(search.CheckInOffsetDays);
            
            // aantal nachten: standaard ophet geconfigureerde aantal in appsettings
            int nights = search.Nights;
            foreach (Availability day in availability)
            {
                // als de minimale verblijfsduur op de incheckdatum groter is dan het geconfigureerde aantal nachten, wordt de minimale verblijfsduur gebruikt (anders wordt er geen prijs terugegeven)
                if (day.AvailabilityDate == checkIn && day.MinLengthOfStay != null && day.MinLengthOfStay.Value > nights)
                {
                    nights = day.MinLengthOfStay.Value;
                }
            }
            DateOnly checkOut = checkIn.AddDays(nights);

            using JsonDocument doc = await staysPdpSections.GetPageAsync(listing.SourceId, checkIn, checkOut, ct);
            return StaysPdpSectionsParser.Parse(doc, checkIn, checkOut, search.Adults, search.Children, search.Pets);
        }
    }
}
