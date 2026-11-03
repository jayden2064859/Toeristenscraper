using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Toeristenscraper.Scraper.Parsing.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Parsing.Airbnb
{
    public static class StaysPdpSectionsParser
    {
        private record PriceInfo(decimal? TotalPrice, bool? TaxIncluded, decimal? TaxAmount);
        private static readonly CultureInfo DutchCulture = new CultureInfo("nl-NL");

        // regex gebruiken om registratienummers en kvknummers uit een html string te parsen
        private static readonly Regex RegistrationRegex = new Regex(@"<b>\s*(Registratiegegevens|Registratienummer|Licentienummer)\s*</b>\s*<br\s*/?>\s*([^<]+)", RegexOptions.IgnoreCase);
        private static readonly Regex KvkRegex = new Regex(@"(Bedrijfsregistratienummer|KVK-nummer|Nummer handelsregister)\s*:\s*(\d{8})", RegexOptions.IgnoreCase);



        // methode die alle benodigde data uit een StaysPdpSections response parsed
        public static AccommodationDetails Parse(JsonDocument doc, DateOnly checkIn, DateOnly checkOut, int adults, int children, int pets)
        {

            if (doc.RootElement.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array)
            {
                throw new InvalidOperationException("StaysPdpSections: response contains GraphQL errors: " + errors.ToString());
            }

            JsonElement node = GetJsonElementHelper.Get(doc.RootElement, "data", "node")
                ?? throw new InvalidOperationException("StaysPdpSections: missing required field inside JSON-response: data.node (contains the listing details)");

            JsonElement pdpPresentation = GetJsonElementHelper.Get(node, "pdpPresentation")
                ?? throw new InvalidOperationException("StaysPdpSections: missing required field inside JSON-response: data.node.pdpPresentation (contains location, host, quality and descriptions)");

            // propertyType (vergelijkbaar met accommodationType bij Booking.com)
            string? propertyType = null;
            JsonElement? propertyTypeElement = GetJsonElementHelper.Get(node, "propertyType");
            if (propertyTypeElement != null)
            {
                propertyType = propertyTypeElement.Value.GetString();
            }

            // isExactLocation 
            bool? isExactLocation = null;
            JsonElement? isExactLocationElement = GetJsonElementHelper.Get(pdpPresentation, "location", "isExactLocation");
            if (isExactLocationElement != null)
            {
                isExactLocation = isExactLocationElement.Value.GetBoolean();
            }

            // exacte adres (alleen aanwezig als field als IsExactLocation = true)
            string? exactAddress = null;
            JsonElement? exactAddressElement = GetJsonElementHelper.Get(pdpPresentation, "location", "exactAddress", "address");
            if (exactAddressElement != null)
            {
                exactAddress = exactAddressElement.Value.GetString();
            }

            // totaal aantal reviews
            int ? ratingCount = null;
            JsonElement? ratingCountElement = GetJsonElementHelper.Get(pdpPresentation, "quality", "listingRatingStats", "overallRatingStats", "ratingCount");
            if (ratingCountElement != null && int.TryParse(ratingCountElement.Value.GetString(), out int parsedCount))
            {
                ratingCount = parsedCount;
            }

            // gem. reviewscore 
            decimal? ratingAverage = null;
            JsonElement? ratingAverageElement = GetJsonElementHelper.Get(pdpPresentation, "quality", "listingRatingStats", "overallRatingStats", "ratingAverage");
            if (ratingAverageElement != null && ratingCount != 0)
            {
                ratingAverage = ratingAverageElement.Value.GetDecimal();
            }
            else
            {
                ratingAverage = null;
            }

            // superhost
            bool? superhost = null;
            JsonElement? superhostElement = GetJsonElementHelper.Get(pdpPresentation, "hostInfo", "passportData", "isSuperhost");
            if (superhostElement != null)
            {
                superhost = superhostElement.Value.GetBoolean();
            }

            // kvk-nummer
            string? kvkNumber = null;
            string? businessHtml = GetJsonElementHelper.Get(pdpPresentation, "businessDetails", "detailsHtml")?.GetString();
            if (businessHtml != null)
            {
                Match kvkMatch = KvkRegex.Match(businessHtml);
                if (kvkMatch.Success)
                {
                    kvkNumber = kvkMatch.Groups[2].Value;
                }
            }

            // registratienummer
            string? registrationNumber = null;
            string? descriptionHtml = GetJsonElementHelper.Get(pdpPresentation, "descriptions", "longDescriptionHtml", "localizedStringWithTranslationPreference")?.GetString();
            if (descriptionHtml != null)
            {
                Match registrationMatch = RegistrationRegex.Match(descriptionHtml);
                if (registrationMatch.Success)
                {
                    // registratienummer normaliseren
                    string normalized = registrationMatch.Groups[2].Value
                        .Trim() // whitespace verwijderen aan het begin en einde
                        .Replace(" ", "") // spaties verwijderen
                        .ToUpperInvariant(); // uppercase

                    if (IsValidRegistrationNumber(normalized))
                    {
                        registrationNumber = normalized;
                    }
                }
            }

            // voorzieningen van de hele accommodatie 
            List<string> amenities = ParseAmenityGroups(GetJsonElementHelper.Get(pdpPresentation, "amenities", "seeAllAmenitiesGroups"));

            // kamers met prijsobservatie
            List<RoomDetails> rooms = ParseListingWithRoomTypes(doc);
            if (rooms.Count == 0)
            {
                rooms.Add(ParseListingWithoutRooms(doc, node, pdpPresentation));
            }

            return new AccommodationDetails
            {
                AccommodationType = propertyType,
                IsExactLocation = isExactLocation,
                Address = exactAddress,
                ReviewCount = ratingCount,
                AverageReviewScore = ratingAverage,
                Superhost = superhost,
                KvkNumber = kvkNumber,
                RegistrationNumber = registrationNumber,
                Rooms = rooms,
                Amenities = amenities,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                AdultCount = adults,
                ChildCount = children,
                PetCount = pets
            };
        }
        private static int? ParseIntFromString(string? text)
        {
            if (text == null)
            {
                return null;
            }
            string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out int value))
            {
                return value;
            }
            return null;
        }

        // deze methode haalt de numerieke waarde van de kameroppervlaktes op 
        // doordat de geconfigureerde uri 'www.airbnb.nl' is, worden automatisch alle kameroppervlaktes in een response in m² teruggegeven
        private static decimal? ParseRoomSize(string? text)
        {
            if (text == null)
            {
                return null;
            }

            string[] parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2 || parts[1] != "m²" || !decimal.TryParse(parts[0], NumberStyles.Number, DutchCulture, out decimal size)) 
            {
                return null;
            }
            return size;

        }

        // methode die voorzieningen uit de response haalt en teruggeeft als lijst van strings (werkt voor  beide accommodatie- en kamer-brede voorzieningen)
        private static List<string> ParseAmenityGroups(JsonElement? amenityGroupsElement)
        {
            List<string> amenities = new List<string>();
            if (amenityGroupsElement == null || amenityGroupsElement.Value.ValueKind != JsonValueKind.Array)
            {
                return amenities;
            }

            foreach (JsonElement group in amenityGroupsElement.Value.EnumerateArray())
            {
                JsonElement? items = GetJsonElementHelper.Get(group, "amenities");
                if (items == null || items.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (JsonElement amenity in items.Value.EnumerateArray())
                {
                    JsonElement? availableElement = GetJsonElementHelper.Get(amenity, "available"); // elke voorziening heeft een 'available' field
                    string? title = GetJsonElementHelper.Get(amenity, "title")?.GetString();
                    if (availableElement != null && availableElement.Value.GetBoolean() && title != null)
                    {
                        amenities.Add(title);
                    }
                }
            }
            return amenities;
        }

        // kamertypen ophalen, alleen accommodaties met meerdere kamertypen (zoals hotels) hebben de sectie PROPERTY_AVAILABLE_ROOMS in de response
        private static List<RoomDetails> ParseListingWithRoomTypes(JsonDocument doc)
        {
            List<RoomDetails> rooms = new List<RoomDetails>();

            JsonElement? sections = GetJsonElementHelper.Get(doc.RootElement, "data", "presentation", "stayProductDetailPage", "sections", "sbuiData", "sectionConfiguration", "root", "sections");
            if (sections == null)
            {
                return rooms;
            }

            foreach (JsonElement section in sections.Value.EnumerateArray())
            {
                string? sectionId = GetJsonElementHelper.Get(section, "sectionId")?.GetString();
                JsonElement? roomElements = GetJsonElementHelper.Get(section, "sectionData", "rooms");
                if (sectionId != "PROPERTY_AVAILABLE_ROOMS" || roomElements == null)
                {
                    continue;
                }

                foreach (JsonElement room in roomElements.Value.EnumerateArray())
                {
                    // bedden, bijv. ["1 queensize bed", "2 eenpersoonsbedden"]
                    List<string> beds = new List<string>();
                    JsonElement? bedElements = GetJsonElementHelper.Get(room, "roomAttributes", "bedsTypeWithCount");
                    if (bedElements != null)
                    {
                        foreach (JsonElement bed in bedElements.Value.EnumerateArray())
                        {
                            string? bedText = bed.GetString();
                            if (bedText != null)
                            {
                                beds.Add(bedText);
                            }
                        }
                    }

                    // een niet-beschikbare kamer heeft geen prijsopbouw, de prijs blijft dan null
                    PriceInfo price = ParsePrice(GetJsonElementHelper.Get(room, "price", "explanationData"));

                    rooms.Add(new RoomDetails
                    {
                        SourceRoomId = GetJsonElementHelper.Get(room, "id")?.GetString(),
                        RoomType = GetJsonElementHelper.Get(room, "title")?.GetString(),
                        RoomSize = ParseRoomSize(GetJsonElementHelper.Get(room, "roomAttributes", "listingSize")?.GetString()),
                        MaxCapacity = ParseIntFromString(GetJsonElementHelper.Get(room, "roomAttributes", "guestCount")?.GetString()),
                        BedCount = CountBeds(beds),
                        BedType = DescribeBeds(beds),
                        TotalPrice = price.TotalPrice,
                        TaxIncluded = price.TaxIncluded,
                        TaxAmount = price.TaxAmount,
                        Amenities = ParseAmenityGroups(GetJsonElementHelper.Get(room, "roomDetailsPresentation", "previewAmenitiesGroups"))
                    });
                }
            }
            return rooms;
        }

        // accommodatie zonder kamertypen (bijv. een hele woning): de hele accommodatie wordt als één kamer opgeslagen
        private static RoomDetails ParseListingWithoutRooms(JsonDocument doc, JsonElement node, JsonElement pdpPresentation)
        {
            // de prijsopbouw staat in de sectie BOOK_IT_SIDEBAR
            JsonElement? explanationData = null;
            JsonElement? sections = GetJsonElementHelper.Get(doc.RootElement, "data", "presentation", "stayProductDetailPage", "sections", "sections");
            if (sections != null)
            {
                foreach (JsonElement section in sections.Value.EnumerateArray())
                {
                    if (GetJsonElementHelper.Get(section, "sectionId")?.GetString() == "BOOK_IT_SIDEBAR")
                    {
                        explanationData = GetJsonElementHelper.Get(section, "section", "structuredDisplayPrice", "explanationData");
                    }
                }
            }
            PriceInfo price = ParsePrice(explanationData);

            // bedden staan per ruimte in sleepingArrangements
            List<string> beds = new List<string>();
            JsonElement? stops = GetJsonElementHelper.Get(pdpPresentation, "sleepingArrangements", "stops");
            if (stops != null)
            {
                foreach (JsonElement stop in stops.Value.EnumerateArray())
                {
                    JsonElement? descriptions = GetJsonElementHelper.Get(stop, "description", "descriptions");
                    if (descriptions == null)
                    {
                        continue;
                    }
                    foreach (JsonElement description in descriptions.Value.EnumerateArray())
                    {
                        string? bedText = GetJsonElementHelper.Get(description, "text")?.GetString();
                        if (bedText != null)
                        {
                            beds.Add(bedText);
                        }
                    }
                }
            }

            // maximaal aantal gasten van de hele accommodatie
            int? capacity = null;
            JsonElement? capacityElement = GetJsonElementHelper.Get(node, "personCapacity");
            if (capacityElement != null)
            {
                capacity = capacityElement.Value.GetInt32();
            }

            // geen SourceRoomId, kamertype of oppervlakte: die bestaan niet zonder kamertypen
            return new RoomDetails
            {
                MaxCapacity = capacity,
                BedCount = CountBeds(beds),
                BedType = DescribeBeds(beds),
                TotalPrice = price.TotalPrice,
                TaxIncluded = price.TaxIncluded,
                TaxAmount = price.TaxAmount
            };
        }

        private static PriceInfo ParsePrice(JsonElement? explanationData)
        {
            // totalPrice wordt alleen gevuld met een waarde als er in een response een "description": "Totaal" regel aanwezig is.
            // zo niet, dan wordt elk individuele prijsgroep bij elkaar opgeteld met sumofPriceLines
            decimal? totalPrice = null;
            decimal? sumOfPriceLines = null;
            bool? taxIncluded = null;
            decimal? taxAmount = null;

            // een niet-beschikbare kamer heeft geen prijsopbouw
            if (explanationData == null)
            {
                return new PriceInfo(null, null, null);
            }

            // controleren of er een priceDetails object aanwezig is
            JsonElement? priceDetails = GetJsonElementHelper.Get(explanationData.Value, "priceDetails");
            if (priceDetails == null)
            {
                return new PriceInfo(null, null, null);
            }

            foreach (JsonElement priceGroup in priceDetails.Value.EnumerateArray())
            {
                // een disclaimer (bijv. "Deze boeking is niet-restitueerbaar") heeft geen items
                JsonElement? items = GetJsonElementHelper.Get(priceGroup, "items");
                if (items == null)
                {
                    continue;
                }

                foreach (JsonElement item in items.Value.EnumerateArray())
                {
                    string? description = GetJsonElementHelper.Get(item, "description")?.GetString();
                    string? priceString = GetJsonElementHelper.Get(item, "priceString")?.GetString();
                    if (description == null || priceString == null)
                    {
                        continue;
                    }
                    // bijv: € 642,14 wordt parts[0] = "€", parts[1] = "642,14"
                    string[] priceStringParts = priceString.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    
                    // numerieke waarde uit de string omzetten naar een decimaal getal. TryParse geeft true + amount variabele terug als het lukt, anders false
                    if (priceStringParts.Length != 2 || !decimal.TryParse(priceStringParts[1], NumberStyles.Number, DutchCulture, out decimal amount))
                    {                  
                        continue;
                    }

                    // een korting staat als "-€ 193,39", het minteken zit dan aan het euroteken vast
                    if (priceStringParts[0] == "-€")
                    {
                        amount = -amount;
                    }

                    // de regel "Totaal" (alleen bij accommodaties zonder kamertypen) is zelf al de som en wordt niet opgeteld
                    if (description == "Totaal")
                    {
                        totalPrice = amount;
                    }
                    else // bij responses zonder een "Totaal" regel, wordt elke prijsregel opgeteld tot een som
                    {                    
                        if (sumOfPriceLines == null) 
                        {
                            sumOfPriceLines = amount; // eerste prijsregel: som begint bij dit bedrag, daarna wordt elke regel opgeteld (else)
                        }
                        else
                        {
                            sumOfPriceLines += amount;
                        }
                    }

                    // controleren of er een "description": "Belastingen" field is. alleen in dit geval wordt de taxAmount gevuld
                    if (description == "Belastingen")
                    {
                        taxIncluded = true;
                        taxAmount = amount;
                    }
                    else if (description == "Belastingen en toeslagen")
                    {
                        taxIncluded = true;
                    }
                }
            }
            // zonder regel "Totaal" (kamers van hotels) is de totaalprijs de som van alle prijsregels
            if (totalPrice == null)
            {
                totalPrice = sumOfPriceLines;
            }
            return new PriceInfo(totalPrice, taxIncluded, taxAmount);
        }

        // totaal aantal bedden, bijv. ["1 queensize bed", "2 eenpersoonsbedden"] wordt 3
        private static int? CountBeds(List<string> beds)
        {
            if (beds.Count == 0)
            {
                return null;
            }
            int total = 0;
            foreach (string bed in beds)
            {
                int? count = ParseIntFromString(bed);
                if (count != null)
                {
                    total += count.Value;
                }
            }
            return total;
        }

        // bedtypen als één tekst, bijv. "1 queensize bed, 2 eenpersoonsbedden" 
        private static string? DescribeBeds(List<string> beds)
        {
            if (beds.Count == 0)
            {
                return null;
            }
            return string.Join(", ", beds);
        }

        // methode om registratienummers te valideren
        // "NLD" + 4 cijfers (gemeentecode) + 16 hexadecimale tekens
        private static bool IsValidRegistrationNumber(string number)
        {
            if (number.Length != 23 || !number.StartsWith("NLD"))
            {
                return false;
            }

            // positie 3 t/m 6: gemeentecode, alleen cijfers
            for (int i = 3; i < 7; i++)
            {
                if (!char.IsAsciiDigit(number[i]))
                {
                    return false;
                }
            }

            // positie 7 t/m 22: 16 hexadecimale tekens
            for (int i = 7; i < 23; i++)
            {
                if (!char.IsAsciiHexDigit(number[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}

