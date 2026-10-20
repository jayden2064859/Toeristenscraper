using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Discovery;

namespace Toeristenscraper.Scraper.Orchestration
{
    // bepaalt wat er tijdens een scrape-run gebeurt
    public class ScrapeOrchestrator(IEnumerable<IAccommodationSource> sources, 
                                    TileAlgorithm tileAlgorithm,
                                    IOptions<ScraperOptions> options,
                                    ILogger<ScrapeOrchestrator> logger)
    {

        public async Task RunAsync(CancellationToken ct)
        {
            // start van de run: elke run krijgt een run-id, en een starttijd (voor logging van scrapes)
            Guid runId = Guid.NewGuid();
            Stopwatch stopwatch = Stopwatch.StartNew();

            logger.LogInformation("Scrape-run {RunId} started at {StartTime}", runId, DateTime.Now);

            foreach (IAccommodationSource source in sources)
            {
                try
                {
                    // maxDepth bepalen voor elk platform. Configuratie per platform staat in appsettings.json
                    
                    int maxDepth = 5; // standaard fallback waarde voor maxDepth.

                    if (source.PlatformId == 1)
                    {
                        maxDepth = options.Value.Booking.MaxDepth;
                    }
                    else if (source.PlatformId == 2)
                    {
                        maxDepth = options.Value.Airbnb.MaxDepth;
                    }
                    // bij een nieuw toegevoegd platform moet hier een nieuwe else if statement die de maxDepth voor dat platform configureerd


                    // alle accommodaties van dit platform worden hierin opgeslagen, over alle gescrapete gebieden
                    List<DiscoveredAccommodation> platformResults = new List<DiscoveredAccommodation>();

                    // discovery per gebied starten (te scrapen gebieden per run worden geconfigureerd in appsettings.json)
                    foreach (ScrapeArea area in options.Value.Areas)
                    {
                        // method uit TileAlgorithm.cs aanroepen die de discovery (inclusief tegelsplitsing) van accommodaties uitvoert
                        platformResults = await tileAlgorithm.DiscoverAsync(source, area, maxDepth, ct);

                        // lijst van alle opgehaalde accommodatie-listings ontdubbelen en in een nieuwe gefilterde lijst zetten
                        List<DiscoveredAccommodation> deduplicatedList = Deduplicate.DeduplicateScrapedListings(platformResults);

                        // loggen hoeveel unieke accommodaties er opgehaald zijn
                        var uniqueListings = deduplicatedList.Count();
                        logger.LogInformation("Area {area}: {uniqueListings} unique listings retreived", area, uniqueListings);

                        // hier worden de detailoperaties uitgevoerd
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Scrape-run {RunId}: error occurred on {PlatformId}", runId, source.PlatformId);
                }
            }
            // einde run: eindtijd en totale duur van de scraperun loggen.
            stopwatch.Stop();
            logger.LogInformation("Scrape-run {RunId} completed at {EndTime}. Total scrape-run duration: {totalDuration}.", runId, DateTime.Now, stopwatch.Elapsed);
        }
    }



    }
}
