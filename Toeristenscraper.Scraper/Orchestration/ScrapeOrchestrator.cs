using Microsoft.Extensions.Options;
using Npgsql;
using Polly.CircuitBreaker;
using System.Diagnostics;
using System.Net;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Reporting;
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

            DateOnly scrapingDate = DateOnly.FromDateTime(DateTime.Today);
            ScraperunReport report = new ScraperunReport { RunId = runId, StartTime = DateTime.Now, ScrapingDate = scrapingDate };

            // run-id aan elke log toevoegen zodat in Wazuh alle logs op run-id gefiltered kan worden
            using IDisposable? runScope = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });


            logger.LogInformation("Scrape-run {RunId} started at {StartTime:yyyy-MM-dd HH:mm:ss}", runId, report.StartTime);

            foreach (IAccommodationSource source in sources)
            {
                // platformnaam aan elke log tijdens de platform-run toevoegen
                using IDisposable? platformScope = logger.BeginScope(new Dictionary<string, object> { ["Platform"] = source.PlatformName });
                PlatformRunStats platformStats = new PlatformRunStats { PlatformName = source.PlatformName, AreaCount = options.Value.Areas.Count };
                report.Platforms.Add(platformStats);
                logger.LogInformation("{Platform}: scraping started ({Areas} area(s))", source.PlatformName, options.Value.Areas.Count);

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
                            platformStats.FailedTiles += 1;
                            report.FailedTiles.Add($"{source.PlatformName}/{area.Name}/{failedTile.Name}");
                        }
                    }

                    // lijst van alle opgehaalde accommodatie-listings ontdubbelen en in een nieuwe gefilterde lijst zetten
                    List<DiscoveredAccommodation> deduplicatedList = platformResults.DistinctBy(accommodation => accommodation.SourceId).ToList();

                    // aantallen vastleggen voor de samenvatting aan het einde van de run
                    platformStats.FoundListings = platformResults.Count;
                    platformStats.UniqueListings = deduplicatedList.Count;

                    // variabele om het aantal opeenvolgend falende requests bij te houden. wanneer het aantal even groot is als de configureerde max consecutive failed requests in de configuratie, stopt het platform met scrapen
                    int consecutiveFailures = 0;

                    // detailoperaties per listing: een mislukte listing wordt overgeslagen, zodat de rest van de run doorgaat 
                    foreach (DiscoveredAccommodation listing in deduplicatedList)
                    {
                        // alleen listings in Nederland verwerken: Booking.com bevat per listing een landcode waarop gecontroleerd kan worden
                        if (listing.CountryCode != null)
                        {
                            if (!string.Equals(listing.CountryCode, "nl", StringComparison.OrdinalIgnoreCase))
                            {
                                platformStats.ForeignListings += 1;
                                continue;
                            }
                        }
                        // airbnb wordt handmatig gecontroleerd waarbij de coordinaten opgezocht worden in de localityboundary referentietabel 
                        else
                        {
                            bool isDutchListing = await dbWriter.IsDutchListingAsync(listing.Latitude, listing.Longitude, ct);
                            if (!isDutchListing)
                            {
                                platformStats.ForeignListings += 1;
                                continue;
                            }
                        }
                        try
                        {
                            // beschikbaarheid van de listing ophalen 
                            IReadOnlyList<Availability> availabilityDays = await source.GetAvailabilityAsync(listing, ct);
 
                            // detailoperaties van de listing uitvoeren om het datamodel te vullen per listing
                            AccommodationDetails details = await source.GetDetailsAsync(listing, availabilityDays, ct);

                            batch.Add(new ScrapedAccommodation { Accommodation = listing, Availability = availabilityDays, Details = details });

                            if (batch.Count == WriteToDb.BatchSize) 
                            {
                                platformStats.FailedListings += await SaveBatchAsync(dbWriter, source, scrapingDate, batch, platformStats, ct);
                            }
                            consecutiveFailures = 0;
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
                            if (ex is OutdatedHashException || ex is BlockedException) 
                            {
                                throw; 
                            }
                            if (IsDatabaseUnavailable(ex)) // database wordt 5x opnieuw geprobeerd (config in program.cs) daarna stopt het platform met scrapen
                            {
                                throw;
                            }
                            if (ex is HttpRequestException)
                            {
                                logger.LogWarning("Listing {SourceId} on {Platform} skipped: {Error}", listing.SourceId, source.PlatformName, ex.Message);
                            }
                            else
                            {
                                logger.LogWarning(ex, "Listing {SourceId} on {Platform} skipped (unexpected error)", listing.SourceId, source.PlatformName);
                            }
                            platformStats.FailedListings += 1;
                            consecutiveFailures += 1;
                            
                            if (consecutiveFailures >= options.Value.Resilience.MaxConsecutiveFailures)
                            {
                                throw new MaxConsecutiveFailuresException("Number of consecutive failed listings reached the configured maximum (" + options.Value.Resilience.MaxConsecutiveFailures + ")");
                            }
                        }
                    }
                    // laatste batch wegschrijven. het return type van SaveBatchAsync is het aantal gefaalde listings voor het platform waarvoor weggeschreven wordt
                    platformStats.FailedListings += await SaveBatchAsync(dbWriter, source, scrapingDate, batch, platformStats, ct);

                }
                catch (BrokenCircuitException)  // loggen wanneer circuit geopent is (run gestopt for specifiek platform)
                {
                    platformStats.Stopped = true;
                    report.StoppedPlatforms.Add($"{source.PlatformName} (circuit open)");
                    logger.LogError("Scrape-run {RunId}: circuit opened for {Platform}, platform stopped for this run", runId, source.PlatformName);
                }
                catch (Exception ex) 
                {
                    platformStats.Stopped = true;
                    report.StoppedPlatforms.Add($"{source.PlatformName} ({ex.GetType().Name})");

                    // verwachte fouten: melding is genoeg, geen stacktrace nodig
                    if (IsDatabaseUnavailable(ex) || ex is BlockedException || ex is OutdatedHashException || ex is HttpRequestException || ex is MaxConsecutiveFailuresException)
                    {
                        logger.LogError("Scrape-run {RunId}: {Platform} stopped: {Error}", runId, source.PlatformName, ex.Message);
                    }
                    else
                    {
                        logger.LogError(ex, "Scrape-run {RunId}: unexpected error on {Platform}", runId, source.PlatformName);
                    }
                }
            }
            // einde run: eindtijd en totale duur van de scraperun loggen.
            stopwatch.Stop();
            report.Duration = stopwatch.Elapsed;
            report.LogSummary(logger);
        }

        // helper method die de WriteBatchAsync methode aanroept om een batch op te slaan naar de database. de helper method geeft het aantal mislukte weggeschreven listings terug
        private async Task<int> SaveBatchAsync(WriteToDb dbWriter, IAccommodationSource source, DateOnly scrapingDate, List<ScrapedAccommodation> batch, PlatformRunStats platformStats, CancellationToken ct)
        {
            int failed = 0;
            try
            {
                await dbWriter.WriteBatchAsync(source.PlatformId, scrapingDate, batch, platformStats, ct);
                platformStats.ProcessedListings += batch.Count;

            }
            catch (Exception ex)
            {
                if (IsDatabaseUnavailable(ex))
                {
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
                // transiente (tijdelijke) fouten
                if (current is NpgsqlException npgsqlException && npgsqlException.IsTransient)
                {
                    return true;
                }
                // niet-transiente fouten (bijv verkeerd wachtwoord, 28P01) wordt ook opgevangen
                if (current is PostgresException postgresException && postgresException.SqlState.StartsWith("28"))
                {
                    return true;
                }
                current = current.InnerException;
            }
            return false;
        }
    }    
}
