using Microsoft.Extensions.Options;
using Npgsql;
using Polly.CircuitBreaker;
using System.Diagnostics;
using System.Net;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Discovery;
using Toeristenscraper.Scraper.Storage;

namespace Toeristenscraper.Scraper.Orchestration
{
    // bepaalt wat er tijdens een scrape-run gebeurt
    public class ScrapeOrchestrator(IEnumerable<IAccommodationSource> sources, TileAlgorithm tileAlgorithm, IOptions<ScraperOptions> options, ILogger<ScrapeOrchestrator> logger, IServiceScopeFactory scopeFactory)
    {
        public async Task RunAsync(CancellationToken ct)
        {
            // start van de run: elke run krijgt een run-id, en een starttijd (voor logging van scrapes)
            Guid runId = Guid.NewGuid();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // logging-gerelateerde variables
            List<string> stoppedPlatforms = new List<string>();
            List<string> failedTiles = new List<string>();
            int retrievedListings = 0;
            int failedListings = 0;
            int totalListings = 0;
            int skippedForeignListings = 0;
            DateOnly scrapingDate = DateOnly.FromDateTime(DateTime.Today);

            logger.LogInformation("\nScrape-run {RunId} started at {StartTime}\n", runId, DateTime.Now);

            foreach (IAccommodationSource source in sources)
            {
                try
                {
                    // WriteToDb gebruikt de DbContext, die scoped is. De orchestrator wordt vanuit de singleton Worker aangeroepen,
                    // daarom krijgt elk platform een eigen scope en daarmee een eigen DbContext, die na het platform weer wordt opgeruimd
                    await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                    WriteToDb dbWriter = scope.ServiceProvider.GetRequiredService<WriteToDb>();

                    // listings worden per groep opgeslagen, zodat niet elke listing een eigen databaseaanroep krijgt
                    List<ScrapedAccommodation> batch = new List<ScrapedAccommodation>();

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
                    retrievedListings += uniqueListings;
                    totalListings += uniqueListings;
                    logger.LogInformation("\nPlatform {Platform}: {UniqueListings} unique listings retrieved", source.PlatformName, uniqueListings);

                    int foreignListings = 0;
                    // detailoperaties per listing: een mislukte listing wordt overgeslagen, zodat de rest van de run doorgaat 
                    foreach (DiscoveredAccommodation listing in deduplicatedList)
                    {
                        // alleen listings in Nederland verwerken (de klanten zijn Nederlandse gemeenten). Zonder landcode field (Airbnb) wordt de listing wel verwerkt
                        if (listing.CountryCode != null && !string.Equals(listing.CountryCode, "nl", StringComparison.OrdinalIgnoreCase))
                        {
                            foreignListings += 1;
                            continue;
                        }

                        try
                        {
                            IReadOnlyList<Availability> availabilityDays = await source.GetAvailabilityAsync(listing, ct);
                            logger.LogDebug("\nAvailabilityCalendar (Listing {SourceId}): {Days} days, from {First} to {Last}, {Available} available", listing.SourceId, availabilityDays.Count, availabilityDays.Min(day => day.AvailabilityDate), availabilityDays.Max(day => day.AvailabilityDate), availabilityDays.Count(day => day.IsAvailable == true));

                            batch.Add(new ScrapedAccommodation { Accommodation = listing, Availability = availabilityDays });

                            if (batch.Count == WriteToDb.BatchSize) 
                            {
                                failedListings += await SaveBatchAsync(dbWriter, source, scrapingDate, batch, ct);
                            }
                        }
                        catch (BrokenCircuitException)
                        {                         
                            throw;
                        }
                        catch (Exception ex)
                        {
                            if (ex is HttpRequestException httpException && httpException.StatusCode == HttpStatusCode.NotFound)
                            {
                                throw; // platform stopt met scrapen bij een 404 (not found)
                            }
                            if (IsDatabaseUnavailable(ex))
                            {
                                throw;
                            }
                            logger.LogWarning(ex, "Listing {SourceId} on {Platform} skipped", listing.SourceId, source.PlatformName);
                            failedListings += 1;
                        }
                    }
                    totalListings -= foreignListings;
                    skippedForeignListings += foreignListings;

                    // laatste batch wegschrijven
                    int result = await SaveBatchAsync(dbWriter, source, scrapingDate, batch, ct);
                    failedListings += result; // aantal mislukte listings uit de SaveBatchAsync methode toevoegen aan de variabele voor het totaal aantal mislukte listings

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
            if (stoppedPlatforms.Count == sources.Count() || totalListings > 0 && failedListings == totalListings)
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

            logger.LogInformation("\nScrape-run {RunId} completed at {EndTime} with status {RunStatus}.\nStopped platforms: {StoppedPlatforms}.\nFailed tiles: {FailedTiles}.\nTotal unique listings retrieved: {RetrievedListings}\nForeign listings skipped: {SkippedForeignListings}.\nTotal listings (filtered): {TotalListings}.\nFailed listings: {FailedListings}.\nDuration: {TotalDuration}.",
            runId, DateTime.Now, runStatus, stoppedPlatforms, failedTiles, retrievedListings, skippedForeignListings, totalListings, failedListings, stopwatch.Elapsed);

        }

        // helper method die de WriteBatchAsync methode aanroept om een batch op te slaan naar de database. de helper method geeft het aantal mislukte weggeschreven listings terug
        private async Task<int> SaveBatchAsync(WriteToDb dbWriter, IAccommodationSource source, DateOnly scrapingDate, List<ScrapedAccommodation> batch, CancellationToken ct)
        {
            int failed = 0;
            try
            {
                await dbWriter.WriteBatchAsync(source.PlatformId, scrapingDate, batch, ct);
            }
            catch (Exception ex)
            {
                if (IsDatabaseUnavailable(ex))
                {
                    logger.LogError(ex, "Database unavailable, batch of {Count} listings on {Platform} not saved: {Error}", batch.Count, source.PlatformName, ex.InnerException?.Message ?? ex.Message);
                    throw;
                }
                logger.LogError(ex, "Batch of {Count} listings on {Platform} not saved: {Error}", batch.Count, source.PlatformName, ex.InnerException?.Message ?? ex.Message);
                failed = batch.Count;
            }
            batch.Clear();
            return failed;
        }

        // helper method die controleert of een fout (of een inner exception) wordt veroorzaakt door een onbereikbare database
        private static bool IsDatabaseUnavailable(Exception ex)
        {
            Exception? current = ex;
            while (current != null)
            {
                if (current is NpgsqlException npgsqlException && npgsqlException.IsTransient)
                {
                    return true;
                }
                current = current.InnerException;
            }
            return false;
        }
    }    
}
