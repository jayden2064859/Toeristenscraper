using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Text;
using Toeristenscraper.Analytics.Models;
using Toeristenscraper.Analytics.Queries;
using Toeristenscraper.Data.Entities;

namespace Toeristenscraper.Analytics.Services
{
    // service die de bezettingsgraad berekent. maakt gebruik van availabilityqueries.cs, dat de meest recente records teruggeeft op basis van meegegeven filters
    public class OccupancyService(AvailabilityQueries availabilityQueries)
    {

        public async Task<OccupancyResult> CalculateAsync(IQueryable<LatestObservation> observations, InsightFilter filter, CancellationToken ct)
        {
            var perAccommodation = await observations
             .GroupBy(o => new { o.platformId, o.sourceId })
             .Select(g => new
             {
                 TotalDays = g.Count(),
                 OccupiedDays = g.Count(o => !o.isAvailable),
                 FirstScrapingDate = g.Min(o => o.scrapingDate),
                 LastScrapingDate = g.Max(o => o.scrapingDate)
             })
             .ToListAsync(ct);

            // bezettingsgraad per accommodatie, het gemiddelde daarvan is de bezettingsgraad van de groep 
            List<decimal> ratesPerAccommodation = new List<decimal>();
            foreach (var accommodation in perAccommodation)
            {
                ratesPerAccommodation.Add((decimal)accommodation.OccupiedDays / accommodation.TotalDays);
            }

            decimal? occupancyRate = null;
            DateOnly? firstScrapingDate = null;
            DateOnly? lastScrapingDate = null;

            if (ratesPerAccommodation.Count > 0)
            {
                occupancyRate = ratesPerAccommodation.Average();
                firstScrapingDate = perAccommodation.Min(a => a.FirstScrapingDate);
                lastScrapingDate = perAccommodation.Max(a => a.LastScrapingDate);
            }

            // applicatieversie bepalen (herleidbaarheid naar codeversie)
            string applicationVersion = typeof(OccupancyService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "onbekend";

            InsightMetadata metadata = new InsightMetadata(filter, firstScrapingDate, lastScrapingDate, DateTime.UtcNow, applicationVersion);
            return new OccupancyResult(occupancyRate, perAccommodation.Count, metadata);
        }

        // aparte methode om de definitieve bezetting (verstreken dagen op het moment wanneer het inzicht berekent wordt, die veranderen gegarandeerd niet meer) 
        public Task<OccupancyResult> CalculateDefinitiveOccupancyRateAsync(InsightFilter filter, CancellationToken ct)
        {
            IQueryable<LatestObservation> observations = availabilityQueries.GetLatestObservations(filter);
            observations = observations.Where(o => o.availabilityDate < filter.referenceDate);
            return CalculateAsync(observations, filter, ct);

        }

        // aparte methode om de bezetting van toekomstige dagen te berekenen. Bij toekomstige datum kan er altijd nog geboekt worden,
        // waardoor de bezettingsgraad kan veranderen, afhankelijk van wanneer de berekening wordt gedaan.

        public Task<OccupancyResult> CalculateForwardOccupancyRateAsync(InsightFilter filter, CancellationToken ct)
        {
            IQueryable<LatestObservation> observations = availabilityQueries.GetLatestObservations(filter);
            observations = observations.Where(o => o.availabilityDate < filter.referenceDate);
            return CalculateAsync(observations, filter, ct);

        }
    }
}

