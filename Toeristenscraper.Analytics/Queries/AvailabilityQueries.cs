using Toeristenscraper.Analytics.Models;
using Toeristenscraper.Data;
using Toeristenscraper.Data.Entities;

namespace Toeristenscraper.Analytics.Queries
{
    public class AvailabilityQueries(ToeristenscraperDbContext dbContext)
    {
        // methode voor een database query op basis van de gekozen filters. returned per listing en beschikbaarheidsdatum het meest recente beschikbaarheidsrecord 
        public IQueryable<LatestObservation> GetLatestObservations(InsightFilter filter)
        {
            // 1. periode en peildatum selecteren
            IQueryable<AccommodationAvailability> query = dbContext.AccommodationAvailability
                .Where(a => a.AvailabilityDate >= filter.fromDate && a.AvailabilityDate <= filter.toDate)
                .Where(a => a.ScrapingDate <= filter.referenceDate);

            // platform alleen filteren als het gevuld is (null = alle platforms)
            if (filter.platformId != null)
            {
                query = query.Where(a => a.AccommodationListing.PlatformId == filter.platformId);
            }

            // gebied alleen filteren als er een naam is gekozen (null = alle gebieden op dit niveau)
            if (filter.regionName != null)
            {
                if (filter.regionLevel == RegionLevel.City)
                {
                    query = query.Where(a => a.AccommodationListing.City == filter.regionName);
                }
                else if (filter.regionLevel == RegionLevel.Municipality)
                {
                    query = query.Where(a => a.AccommodationListing.Municipality == filter.regionName);
                }
                else
                {
                    query = query.Where(a => a.AccommodationListing.Province == filter.regionName);
                }
            }

            // 2. per accommodatie en datum alleen de meest recente observatie
            IQueryable<AccommodationAvailability> latest = query.Where(a => !dbContext.AccommodationAvailability.Any(b =>
                b.AccommodationListing.PlatformId == a.AccommodationListing.PlatformId &&
                b.AccommodationListing.SourceId == a.AccommodationListing.SourceId &&
                b.AvailabilityDate == a.AvailabilityDate &&
                b.ScrapingDate > a.ScrapingDate &&
                b.ScrapingDate <= filter.referenceDate));

            IQueryable<LatestObservation> latestObservations = latest.Select(a => new LatestObservation
            {
                platformId = a.AccommodationListing.PlatformId,
                sourceId = a.AccommodationListing.SourceId,
                availabilityDate = a.AvailabilityDate,
                isAvailable = a.IsAvailable,
                city = a.AccommodationListing.City,
                municipality = a.AccommodationListing.Municipality,
                province = a.AccommodationListing.Province,
                accommodationType = a.AccommodationListing.AccommodationType,
                scrapingDate = a.ScrapingDate
            });
            return latestObservations;

        }
    }
}

