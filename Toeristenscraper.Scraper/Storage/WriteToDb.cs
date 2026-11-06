using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Toeristenscraper.Scraper.Reporting;
using Npgsql;
using Toeristenscraper.Data;
using Toeristenscraper.Data.Entities;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Storage
{
    public class WriteToDb(ToeristenscraperDbContext db, ILogger<WriteToDb> logger)
    {
        public const int BatchSize = 100;
        public async Task WriteBatchAsync(int platformId, DateOnly scrapingDate, IReadOnlyList<ScrapedAccommodation> batch, PlatformRunStats stats, CancellationToken ct)
        {
            if (batch.Count == 0)
            {
                return;
            }

            // lijst van sourceIds van alle opgehaalde listings maken
            List<string> sourceIds = batch.Select(item => item.Accommodation.SourceId).ToList();

            // listings die vandaag al gescraped zijn (bijv. na een herstart): beschikbaarheid en kamers worden niet nog een keer opgeslagen,
            // omdat de beschikbaarheid per listing, datum en scrapedatum uniek is en kamers anders dubbel worden vastgelegd
            List<string> alreadyScrapedToday = await db.AccommodationAvailability
                .Where(a => a.ScrapingDate == scrapingDate && a.AccommodationListing.PlatformId == platformId && sourceIds.Contains(a.AccommodationListing.SourceId))
                .Select(a => a.AccommodationListing.SourceId)
                .Distinct()
                .ToListAsync(ct);

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

            // bestaande vestigingen met de registratienummers uit deze batch ophalen
            List<string> registrationNumbers = new List<string>();
            foreach (ScrapedAccommodation item in batch)
            {
                if (item.Details.RegistrationNumber != null && !registrationNumbers.Contains(item.Details.RegistrationNumber))
                {
                    registrationNumbers.Add(item.Details.RegistrationNumber);
                }
            }

            // bestaande vestigingen (registratienummers) in een dictionary zetten, zodat er opgezocht kan worden of een registratienummer uit de gescrapete batch al in de database staat
            Dictionary<string, AccommodationEstablishment> establishments = await db.AccommodationEstablishment
                .Where(e => registrationNumbers.Contains(e.RegistrationNumber))
                .ToDictionaryAsync(e => e.RegistrationNumber, ct);

            // meest recente set voorzieningen per listing (over alle versies van de listing heen), om wijzigingen te herkennen
            List<AccommodationAmenity> existingAmenities = await db.AccommodationAmenity
                .Include(a => a.AccommodationListing)
                .Where(a => a.AccommodationListing.PlatformId == platformId && sourceIds.Contains(a.AccommodationListing.SourceId))
                .ToListAsync(ct);

            Dictionary<string, HashSet<string>> latestAmenitySets = new Dictionary<string, HashSet<string>>();
            foreach (IGrouping<string, AccommodationAmenity> group in existingAmenities.GroupBy(a => a.AccommodationListing.SourceId))
            {
                DateOnly latestDate = group.Max(a => a.ScrapingDate);
                latestAmenitySets[group.Key] = group.Where(a => a.ScrapingDate == latestDate).Select(a => a.AmenityValue).ToHashSet(); 
            }

            List<AccommodationAvailability> availabilityRows = new List<AccommodationAvailability>();
            int newListings = 0;
            int newReviews = 0;
            int newAmenitySets = 0;
            int newAmenities = 0;
            int newRoomAmenities = 0;
            int newRooms = 0;
            int unchangedListings = 0;
            int skippedAlreadyScraped = 0;
            int listingsWithAvailability = 0;


            // meest recent opgeslagen versie van een listing vergelijken met de huidig gescrapete versie. Als er een waarde gewijzigd is, wordt er een nieuw record gemaakt
            foreach (ScrapedAccommodation item in batch)
            {
                // vestiging opzoeken of aanmaken 
                AccommodationEstablishment? establishment = null;
                string? registrationNumber = item.Details.RegistrationNumber;
                if (registrationNumber != null)
                {
                    // controleren of het registratienummer van de huidig doorlopen listing al in de dictionary van bestaande registratienummers staat
                    establishment = establishments.GetValueOrDefault(registrationNumber);
                    if (establishment == null)
                    {
                        establishment = new AccommodationEstablishment { RegistrationNumber = registrationNumber };
                        db.AccommodationEstablishment.Add(establishment);
                        establishments[registrationNumber] = establishment; // nieuw aangemaakte establishment toevoegen aan de dictionary zodat het niet opnieuw aangemaakt kan worden
                    }
                }
                // woonplaats, gemeente en provincie bepalen met PostGIS (ST_Contains via NetTopologySuite). null wanneer er geen coördinaten zijn of het punt buiten Nederland ligt
                LocalityBoundary? region = await LookupRegionAsync(item.Accommodation.Latitude, item.Accommodation.Longitude, ct);
                AccommodationListing candidate = MapListing(item, platformId, region, establishment, scrapingDate);
               
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
                    unchangedListings += 1;
                    logger.LogDebug("Scraped listing {SourceId} has seen no changes since last saved version, skipped saving to database", candidate.SourceId);
                }

                // review alleen opslaan als het aantal reviews of de gemiddelde score gewijzigd is
                int? reviewCount = item.Details.ReviewCount ?? item.Accommodation.ReviewCount;
                decimal? reviewScore = item.Details.AverageReviewScore ?? item.Accommodation.AverageReviewScore;

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

                if (alreadyScrapedToday.Contains(candidate.SourceId))
                {
                    skippedAlreadyScraped += 1;
                    logger.LogDebug("Listing {SourceId} already scraped on {ScrapingDate}, availability and rooms skipped", candidate.SourceId, scrapingDate);
                    continue;
                }

                // kamerinfo met prijsobservatie
                AccommodationDetails details = item.Details;
                if (details.CheckInDate != null && details.CheckOutDate != null) 
                {
                    foreach (RoomDetails room in details.Rooms)
                    {
                        AccommodationRoom roomRow = new AccommodationRoom
                        {
                            AccommodationListing = listing,
                            SourceRoomId = room.SourceRoomId,
                            RoomType = room.RoomType,
                            RoomSize = room.RoomSize,
                            MaxCapacity = room.MaxCapacity,
                            BedCount = room.BedCount,
                            BedType = room.BedType,
                            TotalPrice = room.TotalPrice,
                            TaxIncluded = room.TaxIncluded,
                            TaxAmount = room.TaxAmount,
                            CheckInDate = details.CheckInDate.Value,
                            CheckOutDate = details.CheckOutDate.Value,
                            AdultCount = details.AdultCount ?? 0,
                            ChildCount = details.ChildCount ?? 0,
                            PetCount = details.PetCount ?? 0,
                            ScrapingDate = scrapingDate
                        };
                        foreach (string amenity in room.Amenities)
                        {
                            newRoomAmenities += 1;
                            roomRow.AccommodationRoomAmenities.Add(new AccommodationRoomAmenity { AmenityValue = amenity, ScrapingDate = scrapingDate });
                        }
                        db.AccommodationRoom.Add(roomRow);
                        newRooms += 1;
                    }
                }

                // accommodatie-brede voorzieningen: alleen een nieuwe set vastleggen als de set gewijzigd is
                HashSet<string> currentAmenities = details.Amenities.ToHashSet();
                HashSet<string>? previousAmenities = latestAmenitySets.GetValueOrDefault(candidate.SourceId);
                if (currentAmenities.Count > 0 && (previousAmenities == null || !previousAmenities.SetEquals(currentAmenities)))
                {
                    foreach (string amenity in currentAmenities)
                    {
                        newAmenities += 1;
                        db.AccommodationAmenity.Add(new AccommodationAmenity { AccommodationListing = listing, AmenityValue = amenity, ScrapingDate = scrapingDate });
                    }
                    newAmenitySets += 1;
                }

                int rowsBefore = availabilityRows.Count;
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
                if (availabilityRows.Count > rowsBefore)
                {
                    listingsWithAvailability += 1;
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
            logger.LogDebug("Batch saved: {Accommodations} accommodations, {NewListings} new or changed listings, {NewRooms} rooms ({NewRoomAmenities} room amenities), {NewReviews} new or changed reviews, {NewAmenitySets} listings with new or changed amenities ({NewAmenities} amenities), {Rows} availability days",
                batch.Count, newListings, newRooms, newRoomAmenities, newReviews, newAmenitySets, newAmenities, availabilityRows.Count);

            // opgeslagen aantallen van deze batch optellen bij de statistieken van het platform
            stats.NewListingVersions += newListings;
            stats.UnchangedListings += unchangedListings;
            stats.SkippedAlreadyScraped += skippedAlreadyScraped;
            stats.NewReviews += newReviews;
            stats.NewRooms += newRooms;
            stats.NewAmenitySets += newAmenitySets;
            stats.NewAmenities += newAmenities;
            stats.NewRoomAmenities += newRoomAmenities;
            stats.AvailabilityRows += availabilityRows.Count;
            stats.ListingsWithAvailability += listingsWithAvailability;
        }


        private static AccommodationListing MapListing(ScrapedAccommodation item, int platformId, LocalityBoundary? region, AccommodationEstablishment? establishment, DateOnly scrapingDate)
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


