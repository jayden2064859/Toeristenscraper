using System.Text.Json;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Parsing.Booking
{
    // parser voor de Apollo-data (JSON) uit de HTML-pagina van een accommodatie op Booking.com 
    // de Apollo-data is een genormaliseerde cache: objecten verwijzen naar elkaar met {"__ref": "Sleutel"}, sommige sleutels bevatten argumenten die per listing verschillen 
    // type, reviews, adres en sterrenclassificatie komen uit FullSearch, deze parser vult alleen kamers, prijzen, voorzieningen, KVK- en registratienummer
    public static class HTMLParser
    {
        private record PriceInfo(decimal? TotalPrice, bool? TaxIncluded, decimal? TaxAmount);
        private record BedInfo(int? BedCount, string? BedType);

        public static AccommodationDetails Parse(JsonDocument doc, DateOnly checkIn, DateOnly checkOut, int adults, int children, int pets)
        {
            JsonElement root = doc.RootElement;

            JsonElement rootQuery = GetJsonElementHelper.Get(root, "ROOT_QUERY")
                ?? throw new InvalidOperationException("Booking.com HTML: missing ROOT_QUERY in Apollo data");

            // bevat de prijzen per aanbieding en de voorzieningen per kamer, ontbreekt als de accommodatie op de zoekdatums volgeboekt is
            JsonElement? roomDetail = FindByPrefix(rootQuery, "roomDetail(");

            // bevat de kamers, voorzieningen van de accommodatie, KVK- en registratienummer
            JsonElement property = FindByPrefix(root, "Property:")
                ?? throw new InvalidOperationException("Booking.com HTML: missing Property in Apollo data");

            // kamers
            List<RoomDetails> rooms = new List<RoomDetails>();
            JsonElement? roomRefs = FindByPrefix(property, "roomsDetails(");
            if (roomRefs != null)
            {
                foreach (JsonElement roomRef in roomRefs.Value.EnumerateArray())
                {
                    // roomRef is {"__ref": "RoomDetails:..."}, Resolve geeft het object waarnaar verwezen wordt
                    JsonElement? roomElement = Resolve(root, roomRef);
                    if (roomElement == null)
                    {
                        continue;
                    }
                    JsonElement room = roomElement.Value;

                    JsonElement? idElement = GetJsonElementHelper.Get(room, "id");
                    if (idElement == null)
                    {
                        continue;
                    }
                    string roomId = idElement.Value.GetInt64().ToString();

                    BedInfo beds = ParseBeds(root, room);

                    // een kamer zonder aanbieding is op de zoekdatums niet beschikbaar, de prijs blijft dan null
                    PriceInfo price = new PriceInfo(null, null, null);
                    List<string> roomAmenities = new List<string>();
                    if (roomDetail != null)
                    {
                        price = ParseCheapestOffer(root, roomDetail.Value, roomId);
                        roomAmenities = ParseRoomAmenities(root, roomDetail.Value, roomId);
                    }

                    rooms.Add(new RoomDetails
                    {
                        SourceRoomId = roomId,
                        RoomType = GetJsonElementHelper.Get(room, "translations", "name")?.GetString(),
                        RoomSize = GetJsonElementHelper.Get(room, "roomSizeM2")?.GetDecimal(),
                        MaxCapacity = GetJsonElementHelper.Get(room, "occupancy", "maxGuests")?.GetInt32(),
                        BedCount = beds.BedCount,
                        BedType = beds.BedType,
                        TotalPrice = price.TotalPrice,
                        TaxIncluded = price.TaxIncluded,
                        TaxAmount = price.TaxAmount,
                        Amenities = roomAmenities
                    });
                }
            }

            return new AccommodationDetails
            {
                KvkNumber = ParseKvkNumber(property),
                RegistrationNumber = ParseRegistrationNumber(property),
                Rooms = rooms,
                Amenities = ParsePropertyAmenities(root, property),
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                AdultCount = adults,
                ChildCount = children,
                PetCount = pets
            };
        }

        // bedden van een kamer, bijv. "1 DOUBLE_BED, 1 SOFA_BED"
        // gewone kamers hebben bedConfigurations (meerdere configuraties zijn alternatieven, de eerste wordt gebruikt),
        // suites en appartementen hebben een lege bedConfigurations en de bedden per ruimte in apartmentRooms
        private static BedInfo ParseBeds(JsonElement root, JsonElement room)
        {
            List<JsonElement> bedElements = new List<JsonElement>();

            JsonElement? configurations = GetJsonElementHelper.Get(room, "bedConfigurations");
            if (configurations != null && configurations.Value.GetArrayLength() > 0)
            {
                JsonElement? beds = GetJsonElementHelper.Get(configurations.Value[0], "beds");
                if (beds != null)
                {
                    foreach (JsonElement bed in beds.Value.EnumerateArray())
                    {
                        bedElements.Add(bed);
                    }
                }
            }
            else
            {
                JsonElement? apartmentRooms = GetJsonElementHelper.Get(room, "apartmentRooms");
                if (apartmentRooms != null)
                {
                    foreach (JsonElement apartmentRoomRef in apartmentRooms.Value.EnumerateArray())
                    {
                        JsonElement? apartmentRoom = Resolve(root, apartmentRoomRef);
                        if (apartmentRoom == null)
                        {
                            continue;
                        }
                        JsonElement? beds = GetJsonElementHelper.Get(apartmentRoom.Value, "beds");
                        if (beds == null)
                        {
                            continue;
                        }
                        foreach (JsonElement bed in beds.Value.EnumerateArray())
                        {
                            bedElements.Add(bed);
                        }
                    }
                }
            }

            if (bedElements.Count == 0)
            {
                return new BedInfo(null, null);
            }

            int total = 0;
            List<string> descriptions = new List<string>();
            foreach (JsonElement bed in bedElements)
            {
                int? count = GetJsonElementHelper.Get(bed, "count")?.GetInt32();
                string? bedType = GetJsonElementHelper.Get(bed, "bedType")?.GetString();
                if (count != null)
                {
                    total += count.Value;
                }
                if (count != null && bedType != null)
                {
                    descriptions.Add(count.Value + " " + bedType);
                }
            }
            return new BedInfo(total, string.Join(", ", descriptions));
        }

        // goedkoopste aanbieding van een kamer: per kamer zijn er meerdere aanbiedingen (tarief, ontbijt, bezetting),
        // de laagste totaalprijs is ook de prijs die Booking.com in de zoekresultaten toont
        private static PriceInfo ParseCheapestOffer(JsonElement root, JsonElement roomDetail, string roomId)
        {
            PriceInfo cheapest = new PriceInfo(null, null, null);

            JsonElement? offerRefs = GetJsonElementHelper.Get(roomDetail, "vMPricingData", "priceDisplayInfoPerOffer");
            if (offerRefs == null)
            {
                return cheapest;
            }

            foreach (JsonElement offerRef in offerRefs.Value.EnumerateArray())
            {
                JsonElement? offer = Resolve(root, offerRef);
                if (offer == null)
                {
                    continue;
                }

                string? offerId = GetJsonElementHelper.Get(offer.Value, "id")?.GetString();
                if (offerId == null || !offerId.StartsWith(roomId + "_", StringComparison.Ordinal))
                {
                    continue;
                }

                JsonElement? breakdown = GetJsonElementHelper.Get(offer.Value, "priceDisplayInfoRL", "defaultBreakdown");
                if (breakdown == null)
                {
                    continue;
                }

                decimal? totalPrice = GetJsonElementHelper.Get(breakdown.Value, "totalPrice", "amountPerStay", "amountUnformatted")?.GetDecimal();
                if (totalPrice == null)
                {
                    continue;
                }

                if (cheapest.TotalPrice == null || totalPrice.Value < cheapest.TotalPrice.Value)
                {
                    cheapest = ParseOfferPrice(breakdown.Value, totalPrice.Value);
                }
            }
            return cheapest;
        }

        // totaalprijs en toeristenbelasting van één aanbieding
        // chargesIncluded: belastingen die in de totaalprijs zitten, chargesExcluded: belastingen die apart betaald worden
        private static PriceInfo ParseOfferPrice(JsonElement breakdown, decimal totalPrice)
        {
            JsonElement? included = GetJsonElementHelper.Get(breakdown, "additionalPriceInformation", "chargesIncluded");
            JsonElement? excluded = GetJsonElementHelper.Get(breakdown, "additionalPriceInformation", "chargesExcluded");

            decimal? includedTax = FindTouristTax(included);
            decimal? excludedTax = FindTouristTax(excluded);

            bool? taxIncluded = null;
            decimal? taxAmount = null;
            if (includedTax != null)
            {
                taxIncluded = true;
                taxAmount = includedTax;
            }
            else if (excludedTax != null)
            {
                taxIncluded = false;
                taxAmount = excludedTax;
            }
            else if (included != null)
            {
                // wel belastingen inbegrepen (bijv. BTW), maar geen aparte regel voor toeristenbelasting
                taxIncluded = true;
            }

            return new PriceInfo(Math.Round(totalPrice, 2), taxIncluded, taxAmount);
        }

        // bedrag van de regel met toeristenbelasting, bijv. "12,5% Toeristenbelasting" of "Toerismetoeslag"
        private static decimal? FindTouristTax(JsonElement? charges)
        {
            if (charges == null)
            {
                return null;
            }

            JsonElement? lines = GetJsonElementHelper.Get(charges.Value, "breakdown");
            if (lines == null || lines.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement line in lines.Value.EnumerateArray())
            {
                JsonElement? copy = GetJsonElementHelper.Get(line, "copy");
                if (copy == null || copy.Value.ValueKind != JsonValueKind.Array || copy.Value.GetArrayLength() == 0)
                {
                    continue;
                }

                string? text = GetJsonElementHelper.Get(copy.Value[0], "text")?.GetString();
                if (text != null && text.Contains("toeris", StringComparison.OrdinalIgnoreCase))
                {
                    decimal? amount = GetJsonElementHelper.Get(line, "amount", "amountUnformatted")?.GetDecimal();
                    if (amount != null)
                    {
                        return Math.Round(amount.Value, 2);
                    }
                }
            }
            return null;
        }

        // voorzieningen van één kamer: categorizedFacilitiesForAllRooms bevat per kamer-id de voorzieningen, gegroepeerd per categorie
        private static List<string> ParseRoomAmenities(JsonElement root, JsonElement roomDetail, string roomId)
        {
            List<string> amenities = new List<string>();

            JsonElement? allRooms = GetJsonElementHelper.Get(roomDetail, "categorizedFacilitiesForAllRooms");
            if (allRooms == null)
            {
                return amenities;
            }

            foreach (JsonElement roomFacilities in allRooms.Value.EnumerateArray())
            {
                JsonElement? idElement = GetJsonElementHelper.Get(roomFacilities, "roomId");
                if (idElement == null || idElement.Value.GetInt64().ToString() != roomId)
                {
                    continue;
                }

                JsonElement? categories = GetJsonElementHelper.Get(roomFacilities, "categorizedFacilities");
                if (categories == null)
                {
                    continue;
                }

                foreach (JsonElement category in categories.Value.EnumerateArray())
                {
                    JsonElement? facilityRefs = GetJsonElementHelper.Get(category, "facilities");
                    if (facilityRefs == null)
                    {
                        continue;
                    }
                    foreach (JsonElement facilityRef in facilityRefs.Value.EnumerateArray())
                    {
                        JsonElement? facility = Resolve(root, facilityRef);
                        if (facility == null)
                        {
                            continue;
                        }
                        string? name = GetJsonElementHelper.Get(facility.Value, "name")?.GetString()?.Trim();
                        if (name != null && !amenities.Contains(name))
                        {
                            amenities.Add(name);
                        }
                    }
                }
            }
            return amenities;
        }

        // voorzieningen van de hele accommodatie: de populairste voorzieningen (highlights) en de blokken facilities 
        // de volledige lijst staat niet in de pagina, die wordt pas bij scrollen geladen via de operatie Facilities
        private static List<string> ParsePropertyAmenities(JsonElement root, JsonElement property)
        {
            List<string> amenities = new List<string>();

            foreach (JsonProperty block in property.EnumerateObject())
            {
                // highlights is een object met entities, facilities is direct een lijst
                JsonElement? facilityRefs = null;
                if (block.Name.StartsWith("highlights(", StringComparison.Ordinal))
                {
                    facilityRefs = GetJsonElementHelper.Get(block.Value, "entities");
                }
                else if (block.Name.StartsWith("facilities(", StringComparison.Ordinal))
                {
                    facilityRefs = block.Value;
                }

                if (facilityRefs == null || facilityRefs.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement facilityRef in facilityRefs.Value.EnumerateArray())
                {
                    // BaseFacility -> instances[0] (verwijzing naar Instance) -> title
                    JsonElement? facility = Resolve(root, facilityRef);
                    if (facility == null)
                    {
                        continue;
                    }
                    JsonElement? instances = GetJsonElementHelper.Get(facility.Value, "instances");
                    if (instances == null || instances.Value.ValueKind != JsonValueKind.Array || instances.Value.GetArrayLength() == 0)
                    {
                        continue; // sommige highlights hebben geen naam, alleen een slug
                    }
                    JsonElement? instance = Resolve(root, instances.Value[0]);
                    if (instance == null)
                    {
                        continue;
                    }
                    string? title = GetJsonElementHelper.Get(instance.Value, "title")?.GetString()?.Trim();
                    if (title != null && !amenities.Contains(title))
                    {
                        amenities.Add(title);
                    }
                }
            }
            return amenities;
        }

        // KVK-nummer uit de gegevens van de handelaar, alleen opgeslagen bij precies 8 cijfers
        // buitenlandse handelsregisters (bijv. HRA38202B) worden zo niet als KVK-nummer opgeslagen
        private static string? ParseKvkNumber(JsonElement property)
        {
            string? number = GetJsonElementHelper.Get(property, "legalInfo", "traderInfo", "contactDetails", "registrationNumber")?.GetString();
            if (number == null)
            {
                return null;
            }

            string normalized = number.Trim().Replace(" ", "");
            if (normalized.Length != 8)
            {
                return null;
            }
            foreach (char c in normalized)
            {
                if (!char.IsAsciiDigit(c))
                {
                    return null;
                }
            }
            return normalized;
        }

        // registratienummer uit licenseData.numbers, genormaliseerd en gevalideerd zoals bij Airbnb
        private static string? ParseRegistrationNumber(JsonElement property)
        {
            JsonElement? numbers = GetJsonElementHelper.Get(property, "licenseData", "numbers");
            if (numbers == null || numbers.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement numberElement in numbers.Value.EnumerateArray())
            {
                if (numberElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                string? number = numberElement.GetString();
                if (number == null)
                {
                    continue;
                }

                string normalized = number.Trim().Replace(" ", "").ToUpperInvariant();
                if (IsValidRegistrationNumber(normalized))
                {
                    return normalized;
                }
            }
            return null;
        }

        // "NLD" + 4 cijfers (gemeentecode) + 16 hexadecimale tekens (gelijk aan de validatie bij Airbnb)
        private static bool IsValidRegistrationNumber(string number)
        {
            if (number.Length != 23 || !number.StartsWith("NLD"))
            {
                return false;
            }
            for (int i = 3; i < 7; i++)
            {
                if (!char.IsAsciiDigit(number[i]))
                {
                    return false;
                }
            }
            for (int i = 7; i < 23; i++)
            {
                if (!char.IsAsciiHexDigit(number[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // zoekt het object op waarnaar een verwijzing {"__ref": "key"} wijst, de sleutel staat als property in de root
        // geeft null terug als het element geen verwijzing is of de sleutel niet in de root staat
        private static JsonElement? Resolve(JsonElement root, JsonElement reference)
        {
            string? key = GetJsonElementHelper.Get(reference, "__ref")?.GetString();
            if (key == null)
            {
                return null;
            }

            if (root.TryGetProperty(key, out JsonElement resolved))
            {
                return resolved;
            }
            return null;
        }

        // geeft de waarde van de eerste property waarvan de naam met de prefix begint (nodig voor sleutels met argumenten die per listing verschillen)
        private static JsonElement? FindByPrefix(JsonElement element, string prefix)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return property.Value;
                }
            }
            return null;
        }
    }
}