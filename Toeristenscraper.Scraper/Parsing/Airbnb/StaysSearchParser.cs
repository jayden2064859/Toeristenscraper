using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.Parsing.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Parsing.Airbnb
{
    public record StaysSearchPage(IReadOnlyList<string> PageCursors, IReadOnlyList<DiscoveredAccommodation> Accommodations);

    public static class StaysSearchParser
    {
        public static StaysSearchPage Parse(JsonDocument doc)
        {
            // results node bevat benodigde page cursors en metadata
            JsonElement results = GetJsonElementHelper.Get(doc.RootElement, "data", "presentation", "staysSearch", "results")
                ?? throw new InvalidOperationException("StaysSearch: missing required field inside JSON-response: data.presentation.staysSearch.results (contains the entire search results data)");

            // searchResults bevat de daadwerkelijke zoekresultaten (listings)
            JsonElement searchResults = GetJsonElementHelper.Get(results, "searchResults")
                ?? throw new InvalidOperationException("StaysSearch: missing required field: searchResults");

            // page cursors uitlezen (bepalen of de limiet bereikt is en welke pagina's opgehaald moeten worden)
            JsonElement pageCursorsElement = GetJsonElementHelper.Get(results, "paginationInfo", "pageCursors")
                ?? throw new InvalidOperationException("StaysSearch: missing required field inside JSON-response: paginationInfo.pageCursors (needed to determine tile-splitting and pagination)");

            List<string> pageCursors = new List<string>();
            foreach (JsonElement cursorElement in pageCursorsElement.EnumerateArray())
            {
                string? cursor = cursorElement.GetString();
                if (cursor != null)
                {
                    pageCursors.Add(cursor);
                }
            }

            // metadata ophalen (nodig voor subsequente api-operaties)
            string federatedSearchId = GetJsonElementHelper.Get(results, "loggingMetadata", "legacyLoggingContext", "federatedSearchId")?.GetString()
                ?? throw new InvalidOperationException("StaysSearch: missing required field inside JSON-response: loggingMetadata.legacyLoggingContext.federatedSearchId (needed for StaysPdpSections operation)");

            // lijst initialiseren voor elke listing binnen de response
            List<DiscoveredAccommodation> accommodations = new List<DiscoveredAccommodation>();

            // alle benodigde fields per listing ophalen, wordt gebruikt om DiscoveredAccommodation instanties te maken
            foreach (JsonElement result in searchResults.EnumerateArray())
            {
                // resultaten zonder een demandStayListingId worden overgeslagen 
                JsonElement? idElement = GetJsonElementHelper.Get(result, "demandStayListing", "id");
                if (idElement == null)
                {
                    continue;
                }

                // bron-id is base64 encoded in de StaysSearch response naar "DemandStayListing:<id>"
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(idElement.Value.GetString()!));
                // alleen het id wordt opgeslagen 
                int separator = decoded.LastIndexOf(':');
                if (separator < 0)
                {
                    continue;
                }
                string sourceId = decoded.Substring(separator + 1);

                // title 
                string? title = null;
                JsonElement? titleElement = GetJsonElementHelper.Get(result, "nameLocalized", "localizedStringWithTranslationPreference");
                if (titleElement != null)
                {
                    title = titleElement.Value.GetString();
                }

                // latitude en longitude 
                double? latitude = null;
                JsonElement? latitudeElement = GetJsonElementHelper.Get(result, "demandStayListing", "location", "coordinate", "latitude");
                if (latitudeElement != null)
                {
                    latitude = latitudeElement.Value.GetDouble();
                }

                double? longitude = null;
                JsonElement? longitudeElement = GetJsonElementHelper.Get(result, "demandStayListing", "location", "coordinate", "longitude");
                if (longitudeElement != null)
                {
                    longitude = longitudeElement.Value.GetDouble();
                }

                // per listing de 'badges' array uitlezen, waar een SUPERHOST badge ik kan staan
                bool? superhost = null;
                JsonElement? badges = GetJsonElementHelper.Get(result, "badges");
                if (badges != null && badges.Value.ValueKind == JsonValueKind.Array)
                {
                    superhost = false;
                    foreach (JsonElement badgeElement in badges.Value.EnumerateArray())
                    {
                        string? badgeType = GetJsonElementHelper.Get(badgeElement, "loggingContext", "badgeType")?.GetString();
                        if (badgeType == "SUPERHOST")
                        {
                            superhost = true;
                        }

                    }
                }

                // alle listings aan een object koppelen (opgeslagen in een lijst) 
                accommodations.Add(new DiscoveredAccommodation
                {
                    // alle fields uit het datamodel die uit StaysSearch op te halen zijn
                    SourceId = sourceId,
                    Title = title,
                    Latitude = latitude,
                    Longitude = longitude,
                    Superhost = superhost,
                    URL = $"https://www.airbnb.nl/rooms/{sourceId}",

                    DetailParameters = new Dictionary<string, string>
                    {
                        { "federatedSearchId", federatedSearchId }
                    }
                });


            }
            return new StaysSearchPage(pageCursors, accommodations);
        }
    }
}


