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

            List<string> stoppedPlatforms = new List<string>();
            List<string> failedTiles = new List<string>();
            int failedListings = 0;
            int totalListings = 0;

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
                        var (accommodations, areaFailedTiles) = await tileAlgorithm.DiscoverAsync(source, area, source.MaxDepth, ct);
                        platformResults.AddRange(accommodations);
                        foreach (Tile failedTile in areaFailedTiles)
                        {
                            failedTiles.Add($"{source.PlatformName}/{area.Name}/{failedTile.Name}");
                        }
                    }

                    // lijst van alle opgehaalde accommodatie-listings ontdubbelen en in een nieuwe gefilterde lijst zetten
                    List<DiscoveredAccommodation> deduplicatedList = platformResults.DistinctBy(accommodation => accommodation.SourceId).ToList();

                    // loggen hoeveel unieke accommodaties er opgehaald zijn
                    int uniqueListings = deduplicatedList.Count;
                    totalListings += uniqueListings;
                    logger.LogInformation("\nPlatform {Platform}: {UniqueListings} unique listings retrieved", source.PlatformName, uniqueListings);

                    // detailoperaties per listing: een mislukte listing wordt overgeslagen, zodat de rest van de run doorgaat 
                    foreach (DiscoveredAccommodation listing in deduplicatedList)
                    {
                        try
                        {
                            IReadOnlyList<Availability> availabilityDays = await source.GetAvailabilityAsync(listing, ct);
                            logger.LogDebug("Listing {SourceId}: {Days} availability days retrieved", listing.SourceId, availabilityDays.Count);

                            // later hier opslaan in de database
                        }
                        catch (BrokenCircuitException)
                        {
                            
                            throw;
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Listing {SourceId} on {Platform} skipped", listing.SourceId, source.PlatformName);
                            failedListings += 1;
                        }
                    }

                }
                catch (BrokenCircuitException ex)  // loggen wanneer circuit geopent is (run gestopt for specifiek platform)
                {
                    stoppedPlatforms.Add($"{source.PlatformName} (circuit open)");
                    logger.LogError(ex, "Scrape-run {RunId}: circuit opened for {Platform}, platform stopped for this run", runId, source.PlatformName);
                }
                catch (Exception ex) 
                {
                    stoppedPlatforms.Add($"{source.PlatformName} ({ex.GetType().Name})");
                    logger.LogError(ex, "Scrape-run {RunId}: error occurred on {Platform}", runId, source.PlatformName);
                }
            }
            // einde run: eindtijd en totale duur van de scraperun loggen.
            stopwatch.Stop();

            // loggen of de scraperuns succesvol zijn uitgevoerd
            string runStatus;
            if (stoppedPlatforms.Count == sources.Count() || failedListings == totalListings)
            {
                runStatus = "Failed"; // geen enkel platform heeft de run afgemaakt
            }
            else if (stoppedPlatforms.Count > 0 || failedTiles.Count > 0 || failedListings > 0)
            {
                runStatus = "PartiallyFailed";   
            }
            else
            {
                runStatus = "Succeeded";
            }

            // "None" tonen in de logregel als er niets gestopt of mislukt is
            if (stoppedPlatforms.Count == 0)
            {
                stoppedPlatforms.Add("None");
            }
            if (failedTiles.Count == 0)
            {
                failedTiles.Add("None");
            }

            logger.LogInformation("\nScrape-run {RunId} completed at {EndTime} with status {RunStatus}.\nStopped platforms: {stoppedPlatforms}.\nFailed tiles: {FailedTiles}.\nTotal (unique) listings: {TotalListings}\nFailed listings: {FailedListings}.\nDuration: {TotalDuration}.",
                                runId, DateTime.Now, runStatus, stoppedPlatforms, failedTiles, totalListings, failedListings, stopwatch.Elapsed);

        }
    }    
}
