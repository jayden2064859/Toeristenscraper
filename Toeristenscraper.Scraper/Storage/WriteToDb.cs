using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using Toeristenscraper.Data;
using Toeristenscraper.Data.Entities;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Storage
{
    public class WriteToDb(ToeristenscraperDbContext db, ILogger<WriteToDb> logger)
    {
        public const int BatchSize = 100;
        public async Task WriteBatchAsync(int platformId, DateOnly scrapingDate, IReadOnlyList<ScrapedAccommodation> batch, CancellationToken ct)
        {
            if (batch.Count == 0)
            {
                return;
            }
            // lijst van sourceIds van alle opgehaalde listings maken
            List<string> sourceIds = batch.Select(item => item.Accommodation.SourceId).ToList();

            // meest recent opgeslagen versie van een AccommodationListing-record uit de database ophalen
            Dictionary<string, AccommodationListing> latestListings = await db.AccommodationListing
                .Where(l => l.PlatformId == platformId && sourceIds.Contains(l.SourceId))
                .GroupBy(l => l.SourceId)
                .Select(g => g.OrderByDescending(l => l.ScrapingDate)
                .ThenByDescending(l => l.AccommodationListingId) // deze is alleen nodig als er meerdere versies gescraped worden op één dag.  
                .First())
                .ToDictionaryAsync(l => l.SourceId, ct);

            // alle reviews van de listings in deze groep ophalen (inclusief de listing, voor de SourceId)
            List<AccommodationReview> existingReviews = await db.AccommodationReview
                .Include(r => r.AccommodationListing)
                .Where(r => r.AccommodationListing.PlatformId == platformId && sourceIds.Contains(r.AccommodationListing.SourceId))
                .ToListAsync(ct);

            // per listing de meest recente review bewaren, over alle versies van de listing heen
            Dictionary<string, AccommodationReview> latestReviews = existingReviews
                .GroupBy(r => r.AccommodationListing.SourceId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ScrapingDate)
                    .ThenByDescending(r => r.AccommodationReviewId)
                    .First());

            List<AccommodationAvailability> availabilityRows = new List<AccommodationAvailability>();
            int newListings = 0;
            int newReviews = 0;


            // meest recent opgeslagen versie van een listing vergelijken met de huidig gescrapete versie. Als er een waarde gewijzigd is, wordt er een nieuw record gemaakt
            foreach (ScrapedAccommodation item in batch)
            {
                LocalityBoundary? region = await LookupRegionAsync(item.Accommodation.Latitude, item.Accommodation.Longitude, ct);
                AccommodationListing candidate = MapListing(item.Accommodation, platformId, region, scrapingDate);
               
                // laatst opgeslagen versie opzoeken, null als de listing nog niet in de database staat
                AccommodationListing? latest = latestListings.GetValueOrDefault(candidate.SourceId);

                AccommodationListing listing;
                if (latest == null || HasListingChanged(latest, candidate))
                {
                    db.AccommodationListing.Add(candidate);
                    listing = candidate;
                    newListings += 1;
                }
                else
                {
                    listing = latest;
                    logger.LogDebug("Scraped listing {SourceId} has seen no changes since last saved version, skipped saving to database", candidate.SourceId);
                }

                // review alleen opslaan als het aantal reviews of de gemiddelde score gewijzigd is
                int? reviewCount = item.Accommodation.ReviewCount;
                decimal? reviewScore = item.Accommodation.AverageReviewScore;

                if (reviewCount != null || reviewScore != null)
                {
                    // laatst opgeslagen review opzoeken, null als er nog geen review is
                    AccommodationReview? latestReview = latestReviews.GetValueOrDefault(candidate.SourceId);

                    if (latestReview == null || latestReview.ReviewCount != reviewCount || latestReview.AverageReviewScore != reviewScore)
                    {
                        // nieuwe review toevoegen
                        db.AccommodationReview.Add(new AccommodationReview
                        {
                            AccommodationListing = listing,
                            ReviewCount = reviewCount,
                            AverageReviewScore = reviewScore,
                            ScrapingDate = scrapingDate
                        });
                        newReviews += 1;
                    }
                }

                // beschikbaarheid: dagen zonder datum, beschikbaarheid of minimaal aantal nachten kunnen niet opgeslagen worden (verplichte kolommen)
                foreach (Availability day in item.Availability)
                {
                    if (day.AvailabilityDate == null || day.IsAvailable == null || day.MinLengthOfStay == null)
                    {
                        continue;
                    }

                    availabilityRows.Add(new AccommodationAvailability
                    {
                        AccommodationListing = listing,
                        AvailabilityDate = day.AvailabilityDate.Value,
                        IsAvailable = day.IsAvailable.Value,
                        MinNights = day.MinLengthOfStay.Value,
                        MaxNights = day.MaxLengthOfStay, // null bij Booking.com
                        ScrapingDate = scrapingDate
                    });
                }
            }
            db.AccommodationAvailability.AddRange(availabilityRows);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
            logger.LogDebug("Batch saved: {Accommodations} accommodations, {NewListings} new listing versions, {NewReviews} new reviews, {Rows} availability rows",
               batch.Count, newListings, newReviews, availabilityRows.Count);     
        }

        private static AccommodationListing MapListing(DiscoveredAccommodation accommodation, int platformId, LocalityBoundary? region, DateOnly scrapingDate)
        {
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
                AccommodationType = accommodation.AccommodationType,
                Coordinates = coordinates,
                IsExactLocation = accommodation.IsExactLocation,
                City = region?.City,
                Municipality = region?.Municipality,
                Province = region?.Province,
                Address = accommodation.Address,
                StarClassification = accommodation.StarClassification,
                Superhost = accommodation.Superhost,
                URL = accommodation.URL,
                ScrapingDate = scrapingDate

                // kvknummer en AccommodationEstablishmentId (registratienummers) missen hier nog, wordt toegevoegd zodra de HTML-bron uitgewerkt wordt
            };
            return accommodationListing;
        }


        // woonplaats, gemeente en provincie bepalen met PostGIS (ST_Contains via NetTopologySuite).
        // null wanneer er geen coördinaten zijn of het punt buiten Nederland ligt
        private async Task<LocalityBoundary?> LookupRegionAsync(double? latitude, double? longitude, CancellationToken ct)
        {
            if (latitude == null || longitude == null)
            {
                return null;
            }

            Point point = new Point(longitude.Value, latitude.Value) { SRID = 4326 };

            return await db.LocalityBoundary
                .AsNoTracking() // ef core hoeft deze rijen niet bij te houden (read-only data)
                .Where(w => w.Geom.Contains(point))
                .FirstOrDefaultAsync(ct);
        }

        // methode die de meest recent bestaande (in de database opgeslagen) versie van een listing te vergelijken met de versie die momenteel gescraped is. 
        // als er een wijziging in data is, geeft deze method true terug en wordt de nieuwe versie opgeslagen
        private static bool HasListingChanged(AccommodationListing latestExisting, AccommodationListing candidate)
        {
            if (candidate.Title != latestExisting.Title || candidate.AccommodationType != latestExisting.AccommodationType ||
                !SameCoordinates(candidate.Coordinates, latestExisting.Coordinates) || candidate.IsExactLocation != latestExisting.IsExactLocation || candidate.Address != latestExisting.Address ||
                candidate.StarClassification != latestExisting.StarClassification || candidate.Superhost != latestExisting.Superhost || candidate.URL != latestExisting.URL ||
                candidate.KVKNumber != latestExisting.KVKNumber || candidate.AccommodationEstablishmentId != latestExisting.AccommodationEstablishmentId ||
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

        // methode voor airbnb om te checken of een listing binnen Nederland ligt 
        // hiervoor wordt de referentietabel LocalityBoundary gebruikt om de gebiedsindeling te bepalen voor elke listing 
        
        public async Task<bool> IsDutchListingAsync(double? latitude, double? longitude, CancellationToken ct)
        {
            // de referentietabel bevat alleen data over Nederland. Elke listing die coordinaten buiten NL bevat, wordt als null teruggegeven hier
            LocalityBoundary? region = await LookupRegionAsync(latitude, longitude, ct);
            return region != null;
        }
    }
}


