using Toeristenscraper.Data.Entities;
using NetTopologySuite.Geometries;
using Toeristenscraper.Scraper.Models;

namespace Toeristenscraper.Scraper.Storage
{
    // aparte (static omdat het niet geinstantieerd hoeft te worden) service class die het omzetten en vergelijken van listings uitvoert
    public static class ListingMapper
    {
        // methode die een DiscoveredAccommodation en AccommodatilDetails object omzet naar een AccommodationListing object, het daadwerkelijke entity object dat weggeschreven kan worden naar de database
        public static AccommodationListing MapListing(ScrapedAccommodation item, int platformId, LocalityBoundary? region, AccommodationEstablishment? establishment, DateOnly scrapingDate)
        {
            DiscoveredAccommodation accommodation = item.Accommodation;
            AccommodationDetails details = item.Details;

            Point? coordinates = null;
            if (accommodation.Latitude != null && accommodation.Longitude != null)
            {
                coordinates = new Point(accommodation.Longitude.Value, accommodation.Latitude.Value) { SRID = 4326 };
            }

            var accommodationListing = new AccommodationListing
            {
                PlatformId = platformId,
                SourceId = accommodation.SourceId,
                Title = accommodation.Title,
                AccommodationType = details.AccommodationType ?? accommodation.AccommodationType,
                Coordinates = coordinates,
                IsExactLocation = details.IsExactLocation ?? accommodation.IsExactLocation,
                City = region?.City,
                Municipality = region?.Municipality,
                Province = region?.Province,
                Address = details.Address ?? accommodation.Address,  // exacte adres: niet altijd aanwezig, Airbnb toont het vooral bij accommodaties met een exacte locatie (zoals hotels), maar zelfs dan niet altijd.
                StarClassification = accommodation.StarClassification,
                Superhost = details.Superhost ?? accommodation.Superhost,
                KVKNumber = details.KvkNumber,
                AccommodationEstablishment = establishment,
                URL = accommodation.URL,
                ScrapingDate = scrapingDate
            };
            return accommodationListing;
        }

        // methode die de meest recent bestaande (in de database opgeslagen) versie van een listing te vergelijken met de versie die momenteel gescraped is. 
        // als er een wijziging in data is, geeft deze method true terug en wordt de nieuwe versie opgeslagen
        public static bool HasListingChanged(AccommodationListing latestExisting, AccommodationListing candidate)
        {
            if (candidate.Title != latestExisting.Title || candidate.AccommodationType != latestExisting.AccommodationType ||
                !SameCoordinates(candidate.Coordinates, latestExisting.Coordinates) || candidate.IsExactLocation != latestExisting.IsExactLocation || candidate.Address != latestExisting.Address ||
                candidate.StarClassification != latestExisting.StarClassification || candidate.Superhost != latestExisting.Superhost || candidate.URL != latestExisting.URL ||
                candidate.KVKNumber != latestExisting.KVKNumber || candidate.AccommodationEstablishment?.AccommodationEstablishmentId != latestExisting.AccommodationEstablishmentId ||
                candidate.City != latestExisting.City || candidate.Municipality != latestExisting.Municipality || candidate.Province != latestExisting.Province)
            {
                return true;
            }
            return false;
        }

        // != op twee Point-objecten vergelijkt referenties, daarom X en Y zelf vergelijken
        private static bool SameCoordinates(Point? a, Point? b)
        {
            if (a == null || b == null)
            {
                return a == null && b == null;
            }
            return a.X == b.X && a.Y == b.Y;
        }
    }
}





