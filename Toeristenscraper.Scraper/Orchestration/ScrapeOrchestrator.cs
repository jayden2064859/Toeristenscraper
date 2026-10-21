using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using System.Diagnostics;
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

            // lijst aanmaken waar de platformnamen in komen te staan wanneer een scrape-run ervoor stopt, zodat dit concreet gelogd kan worden
            List<string> failedPlatforms = new List<string>();

            logger.LogInformation("\nScrape-run {RunId} started at {StartTime}\n", runId, DateTime.Now);

            foreach (IAccommodationSource source in sources)
            {
                try
                {
                    // alle accommodaties van dit platform worden hierin opgeslagen, over alle gescrapete gebieden
                    List<DiscoveredAccommodation> platformResults = new List<DiscoveredAccommodation>();

                    // discovery per gebied starten (te scrapen gebieden per run worden geconfigureerd in appsettings.json)
                    foreach (ScrapeArea area in options.Value.Areas)
                    {
                        // method uit TileAlgorithm.cs aanroepen die de discovery (inclusief tegelsplitsing) van accommodaties uitvoert.
                        // resultaten per gebied worden toegevoegd aan de platformresults lijst
                        platformResults.AddRange(await tileAlgorithm.DiscoverAsync(source, area, source.MaxDepth, ct));

                        // hier worden de detailoperaties uitgevoerd
                    }

                    // lijst van alle opgehaalde accommodatie-listings ontdubbelen en in een nieuwe gefilterde lijst zetten
                    List<DiscoveredAccommodation> deduplicatedList = platformResults.DistinctBy(accommodation => accommodation.SourceId).ToList();

                    // loggen hoeveel unieke accommodaties er opgehaald zijn
                    int uniqueListings = deduplicatedList.Count;
                    logger.LogInformation("\nPlatform {Platform}: {UniqueListings} unique listings retrieved", source.PlatformName, uniqueListings);

                }
                catch (BrokenCircuitException ex)  // loggen wanneer circuit geopent is (run gestopt for specifiek platform)
                {
                    failedPlatforms.Add(source.PlatformName);
                    logger.LogError(ex, "Scrape-run {RunId}: circuit opened for {Platform}, platform stopped for this run", runId, source.PlatformName);
                }
                catch (Exception ex) 
                {
                    failedPlatforms.Add(source.PlatformName);
                    logger.LogError(ex, "Scrape-run {RunId}: error occurred on {Platform}", runId, source.PlatformName);
                }
            }
            // einde run: eindtijd en totale duur van de scraperun loggen.
            stopwatch.Stop();

            // loggen of de scraperuns succesvol zijn uitgevoerd
            string runStatus;
            if (failedPlatforms.Count == 0)
            {
                runStatus = "Succeeded";
                failedPlatforms.Add("None");
            }
            else if (failedPlatforms.Count < sources.Count())
            {
                runStatus = "PartiallyFailed";
            }
            else
            {
                runStatus = "Failed";
            }

            logger.LogInformation("\nScrape-run {RunId} completed at {EndTime} with status '{runStatus}'. Failed platforms: {FailedPlatforms}. Duration: {totalDuration}.", runId, DateTime.Now, runStatus, failedPlatforms, stopwatch.Elapsed);
                       
        }
    }    
}
