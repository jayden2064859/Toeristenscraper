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
            IQueryable<LatestObservation> latestObservations = query
                .GroupBy(a => new { a.AccommodationListing.PlatformId, a.AccommodationListing.SourceId, a.AvailabilityDate })
                .Select(g => g.OrderByDescending(a => a.ScrapingDate).First())
                .Select(a => new LatestObservation(
                    a.AccommodationListing.PlatformId,
                    a.AccommodationListing.SourceId,
                    a.AvailabilityDate,
                    a.IsAvailable,
                    a.AccommodationListing.City,
                    a.AccommodationListing.Municipality,
                    a.AccommodationListing.Province,
                    a.AccommodationListing.AccommodationType));
            return latestObservations;

        }
    }
}

