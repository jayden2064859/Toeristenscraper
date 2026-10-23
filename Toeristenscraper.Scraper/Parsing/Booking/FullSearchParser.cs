using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.Json;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Booking;

namespace Toeristenscraper.Scraper.Parsing.Booking
{
    // haalt alle benodigde velden van het datamodel uit de FullSearch response 
    // leest nbResultsTotal en de resultaten uit, zet ze om naar DiscoveredAccommodation en vertaalt het accommodatietype met de gemappte enum waarde
    public record FullSearchPage(int NbResultsTotal, IReadOnlyList<DiscoveredAccommodation> Accommodations);
    public static class FullSearchParser
    {
        public static FullSearchPage Parse(JsonDocument doc)
        {
            // de search node ophalen, dit is de root van de resultaten
            JsonElement search = GetJsonElementHelper.Get(doc.RootElement, "data", "searchQueries", "search")
                 ?? throw new InvalidOperationException("FullSearch: missing required field inside JSON-response: data.searchQueries.search (contains the entire search results data)");

            // totaal aantal accommodaties ophalen
            int totalAccommodations = GetJsonElementHelper.Get(search, "pagination", "nbResultsTotal")?.GetInt32()
                ?? throw new InvalidOperationException("FullSearch: missing required field inside JSON-response: pagination.nbResultsTotal (needed to determine tile-splitting for subsequent scrapes)");

            // inhoud van de resultaten ophalen (hier staan alle accommodaties in) 
            JsonElement results = GetJsonElementHelper.Get(search, "results")
                ?? throw new InvalidOperationException("FullSearch: missing required field inside JSON-response: results (containing accommodation listings)");

            // lijst aanmaken waar alle accommodaties in komen te staan
            List<DiscoveredAccommodation> accommodations = new List<DiscoveredAccommodation>();

            // alle benodigde fields ophalen, wordt gebruikt om DiscoveredAccommodation instanties te maken
            foreach (JsonElement result in results.EnumerateArray())
            {
                // verplichte velden die nodig zijn voor subsequente API-operaties. Als een van deze twee ontbreekt, wordt de specifieke listing overgeslagen
                JsonElement? id = GetJsonElementHelper.Get(result, "basicPropertyData", "id"); // source-id
                JsonElement? pageName = GetJsonElementHelper.Get(result, "basicPropertyData", "pageName"); // pageName
                if (id == null || pageName == null)
                {
                    continue;
                }

                // title
                string? title = null;
                JsonElement? titleElement = GetJsonElementHelper.Get(result, "displayName", "text");
                if (titleElement != null)
                {
                    title = titleElement.Value.GetString();
                }

                // latitude
                double? latitude = null;
                JsonElement? latitudeElement = GetJsonElementHelper.Get(result, "basicPropertyData", "location", "latitude");
                if (latitudeElement != null)
                {
                    latitude = latitudeElement.Value.GetDouble();
                }

                // longitude
                double? longitude = null;
                JsonElement? longitudeElement = GetJsonElementHelper.Get(result, "basicPropertyData", "location", "longitude");
                if (longitudeElement != null)
                {
                    longitude = longitudeElement.Value.GetDouble();
                }

                // address
                string? address = null;
                JsonElement? addressElement = GetJsonElementHelper.Get(result, "basicPropertyData", "location", "address");
                
                // eerst checken of de address field uberhaupt aanwezig is voor de listing
                if (addressElement != null)
                {
                    address = addressElement.Value.GetString();
                }
                // als tweede checken of de bestaande field lege tekst bevat
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = null;
                }

                // accommodationType (numeriek)
                int? accommodationTypeId = null;
                JsonElement? accommodationTypeElement = GetJsonElementHelper.Get(result, "basicPropertyData", "accommodationTypeId");
                if (accommodationTypeElement != null)
                {
                    accommodationTypeId = accommodationTypeElement.Value.GetInt32();
                }

                // starClassification
                decimal? starClassification = null;
                JsonElement? starClassificationElement = GetJsonElementHelper.Get(result, "basicPropertyData", "starRating", "value");
                if (starClassificationElement != null)
                {
                    starClassification = starClassificationElement.Value.GetDecimal();
                }

                // reviewCount
                int? reviewCount = null;
                JsonElement? reviewCountElement = GetJsonElementHelper.Get(result, "basicPropertyData", "reviewScore", "reviewCount");
                if (reviewCountElement != null)
                {
                    reviewCount = reviewCountElement.Value.GetInt32();
                }
               
                // averageReviewScore
                decimal? averageReviewScore = null;
                JsonElement? averageReviewScoreElement = GetJsonElementHelper.Get(result, "basicPropertyData", "reviewScore", "score");
                if (averageReviewScoreElement != null)
                {
                    averageReviewScore = averageReviewScoreElement.Value.GetDecimal();
                }
                if (reviewCount == 0)
                {
                    averageReviewScore = null;
                }

                // countryCode 
                string? countryCode = null;
                JsonElement? countryCodeElement = GetJsonElementHelper.Get(result, "basicPropertyData", "location", "countryCode");
                if (countryCodeElement != null)
                {
                    countryCode = countryCodeElement.Value.GetString();
                }

                // IsExactLocation
                var isExactLocation = false; // standaard op false, alleen true voor Booking wanneer er een expliciet address field beschikbaar is 
                if (address != null)
                {
                    isExactLocation = true;
                }

                // URL
                string? url = null;
                if (!string.IsNullOrEmpty(countryCode) && !string.IsNullOrEmpty(pageName.ToString()))
                {
                    url = $"https://www.booking.com/hotel/{countryCode}/{pageName}.html";
                }


                // DiscoveredAccommodation-object aanmaken voor elk gevonden listing
                accommodations.Add(new DiscoveredAccommodation
                {
                    SourceId = id.Value.ToString(),
                    Title = title,
                    Latitude = latitude,
                    Longitude = longitude,
                    Address = address,
                    AccommodationType = MapAccommodationType(accommodationTypeId),
                    StarClassification = starClassification,
                    ReviewCount = reviewCount,
                    AverageReviewScore = averageReviewScore,
                    IsExactLocation = isExactLocation,
                    URL = url,

                    // fields die niet in het datamodel opgeslagen worden, maar wel functioneel nodig zijn voor andere API-operaties
                    DetailParameters = new Dictionary<string, string>
                    {
                        { "countryCode", countryCode ?? string.Empty },
                        { "pageName", pageName.Value.GetString() ?? string.Empty }
                    }
                });
            }
            return new FullSearchPage(totalAccommodations, accommodations);
        }

        // helper method om de numerieke accommodationTypeId te vertalen naar textuele waarde, zoals gemapped AccommodationTypes.cs
        private static string? MapAccommodationType(int? id)
        {
            if (id == null)
            {
                return null;
            }

            else if (Enum.IsDefined(typeof(AccommodationTypes), id.Value))
            {
                return ((AccommodationTypes)id.Value).ToString();
            }
            else
            {
                return $"Onbekend ({id})";
            }
        }
    }
}
