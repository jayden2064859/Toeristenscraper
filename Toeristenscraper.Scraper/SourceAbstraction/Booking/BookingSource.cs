using Microsoft.Extensions.Options;
using System.Text.Json;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Parsing.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Booking.Operations;

namespace Toeristenscraper.Scraper.SourceAbstraction.Booking
{
    // implementeert IAccommodationSource: eerst pagina 1, bij >1000 resultaten meteen “limiet bereikt”, anders de overige pagina’s ophalen via offset
    public class BookingSource(FullSearch fullSearch, AvailabilityCalendar availabilityCalendar,
                                HTML html, IOptions<ScraperOptions> options, ILogger<BookingSource> logger) : IAccommodationSource
    {
        public string PlatformName { get; } = "Booking.com";
        public int PlatformId { get; } = 1;
        public int MaxDepth => options.Value.Booking.MaxDepth;

        public async Task<TileSearchResult> SearchTileAsync(Tile tile, CancellationToken ct)
        {
            // methode uit FullSearch.cs wordt gebruikt om een request naar de API-operatie FullSearch te sturen,
            // en daar de payload van op te halen 
            using var firstDoc = await fullSearch.GetPageAsync(tile, 0, ct);

            // Parse methode uit FullSearchparser.cs wordt gebruikt om een FullSearchPage-object terug te geven,
            // dat een lijst van DiscoveredAccommodation-objecten bevat (alle parsed data voor elke listing uit de FullSearch response)
            var firstPage = FullSearchParser.Parse(firstDoc);

            // LimitReached wordt op true gezet wanneer een response de limiet van accommodatie-listings heeft bereikt
            // wanneer true, wordt het zoekgebied opgesplitst en apart doorzocht.
            if (firstPage.NbResultsTotal > FullSearch.MaxResults)
            {
                return new TileSearchResult(new List<DiscoveredAccommodation>(), LimitReached: true);
            }

            var allAccommodations = new List<DiscoveredAccommodation>();
            allAccommodations.AddRange(firstPage.Accommodations);

            // volgende pagina's ophalen: offset 100, 200, etc tot alle resultaten binnen zijn
            int offset = FullSearch.PageSize;
            while (offset < firstPage.NbResultsTotal)
            {
                using JsonDocument doc = await fullSearch.GetPageAsync(tile, offset, ct);
                FullSearchPage page = FullSearchParser.Parse(doc);
                allAccommodations.AddRange(page.Accommodations);

                // elke subsequente request verhogen met offset +100 totdat het totaal aantal resultaten volledig opgehaald is
                offset = offset + FullSearch.PageSize;
            }

            // tijdens een testrun gaf een tegel nbResults = 0 op met 100 listings retreived. om dit te voorkomen wordt de onderstaande check toegevoegd
            if (firstPage.NbResultsTotal == 0 && firstPage.Accommodations.Count > 0)
            {
                throw new InvalidOperationException(
                    $"FullSearch: Inconsistent response for tile {tile.Name}: nbResultsTotal = 0 while results contain {firstPage.Accommodations.Count} listings");
            }


            logger.LogDebug("Tile {TileName}: nbResultsTotal = {Total}, retrieved = {Count}",
            tile.Name, firstPage.NbResultsTotal, allAccommodations.Count);
            return new TileSearchResult(allAccommodations, LimitReached: false);
        }

        public async Task<IReadOnlyList<Availability>> GetAvailabilityAsync(DiscoveredAccommodation listing, CancellationToken ct)
        {
            string pageName = listing.DetailParameters["pageName"];
            string countryCode = listing.DetailParameters["countryCode"];

            // lijst die alle dagen van alle requests samen bijhoudt
            List<Availability> allDays = new List<Availability>();

            // eerste request is van de huidige dag (scrapedatum) tot 61 dagen vooruit, subsequente requests gebruiken 61 dagen later om de volgende set beschikbaarheidsdatums op te halen
            DateOnly startDate = DateOnly.FromDateTime(DateTime.Today);
            DateOnly endDate = startDate.AddDays(365);

            while (startDate <= endDate)
            {
                using JsonDocument payload = await availabilityCalendar.GetPageAsync(countryCode, pageName, startDate, ct);

                var page = AvailabilityCalendarParser.Parse(payload);
                allDays.AddRange(page.Calendars);

                // startdatum voor de volgende request instellen (61 dagen later als startdatum)
                startDate = startDate.AddDays(AvailabilityCalendar.maxAmountOfDays);
            }
            return allDays;
        }

        public async Task<AccommodationDetails> GetDetailsAsync(DiscoveredAccommodation listing, IReadOnlyList<Availability> availability, CancellationToken ct)
        {
            // search parameter opties uit appsettings worden gebruikt, gelijk aan Airbnb zodat prijzen vergelijkbaar zijn
            SearchParameterOptions search = options.Value.SearchParameters;
            DateOnly checkIn = DateOnly.FromDateTime(DateTime.Today).AddDays(search.CheckInOffsetDays);

            // aantal nachten: standaard het geconfigureerde aantal, of de minimale verblijfsduur op de incheckdatum als die langer is
            int nights = search.Nights;
            foreach (Availability day in availability)
            {
                if (day.AvailabilityDate == checkIn && day.MinLengthOfStay != null && day.MinLengthOfStay.Value > nights)
                {
                    nights = day.MinLengthOfStay.Value;
                }
            }
            DateOnly checkOut = checkIn.AddDays(nights);

            string pageName = listing.DetailParameters["pageName"];

            using JsonDocument doc = await html.GetPageAsync(pageName, checkIn, checkOut, ct);
            return HTMLParser.Parse(doc, checkIn, checkOut, search.Adults, search.Children, search.Pets);
        }
    }
}

