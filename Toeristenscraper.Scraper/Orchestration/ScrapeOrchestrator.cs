using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using System.Diagnostics;
using System.Reflection;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Discovery;
using Toeristenscraper.Scraper.Models;
using Toeristenscraper.Scraper.Reporting;
using Toeristenscraper.Scraper.SourceAbstraction;
using Toeristenscraper.Scraper.Storage;

namespace Toeristenscraper.Scraper.Orchestration
{
    // bepaalt wat er tijdens een scrape-run gebeurt
    public class ScrapeOrchestrator(IEnumerable<IAccommodationSource> sources, TileAlgorithm tileAlgorithm, IOptions<ScraperOptions> options, ILogger<ScrapeOrchestrator> logger, IServiceScopeFactory scopeFactory)
    {
        // zorgt ervoor dat platforms niet tegelijk naar de database schrijven (voorkomt dubbele inserts in de db)
        private readonly SemaphoreSlim dbWriteLock = new SemaphoreSlim(1, 1);
        public async Task RunAsync(CancellationToken ct)
        {

            // start van de run: elke run krijgt een run-id, en een starttijd (voor logging van scrapes)
            Guid runId = Guid.NewGuid();
            // stopwatch die de gehele scrape-run duration bijhoudt (alle platforms samen)
            Stopwatch scrapeRunStopwatch = Stopwatch.StartNew();

            DateOnly scrapingDate = DateOnly.FromDateTime(DateTime.Today);
            ScrapeRunReport report = new ScrapeRunReport { RunId = runId, StartTime = DateTime.Now };

            // run-id aan elke log toevoegen zodat in Wazuh alle logs op run-id gefiltered kan worden
            using IDisposable? runScope = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId });

            logger.LogInformation("Scrape-run {RunId} started at {StartTime:yyyy-MM-dd HH:mm:ss}", runId, report.StartTime);

            // applicatieversie loggen aan het begin van elke scrape-run, zodat achteraf elke scraperun herleidbaar is naar welke codeversie gebruikt is (github repo versie)
            string? applicationVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            logger.LogInformation("Application version: {ApplicationVersion}, Booking.com request delay {BookingDelayMs}ms, Airbnb request delay {AirbnbDelayMs}ms",
                applicationVersion, options.Value.Booking.RequestDelayMs, options.Value.Airbnb.RequestDelayMs);

            // ook aan het begin van elke scrape-run loggen welke polly config gebruikt wordt: maximale retries per listing voordat de listing overgeslagen wordt, de retry-strategie, en cooldown-strategie (max-cooldowns limiet bereikt = platform stopt met scrapen)
            logger.LogInformation("Resilience: max retries {MaxRetries} (initial delay {RetryDelaySeconds}s, exponential backoff), cooldown of {CooldownMinutes} min after {MaxConsecutiveFailures} consecutive failed listings, platform stops after {MaxCooldowns} cooldowns without success",
                options.Value.Resilience.MaxRetries, options.Value.Resilience.RetryDelaySeconds,
                options.Value.Resilience.CooldownMinutes, options.Value.Resilience.MaxConsecutiveFailures, options.Value.Resilience.MaxCooldowns);
            // lijst van tasks aanmaken die uitgevoerd moeten worden in een run (scrape-runs van alle bestaande platforms)
            List<Task> tasks = new List<Task>();
            foreach (IAccommodationSource source in sources)
            {
                PlatformRunStats platformStats = new PlatformRunStats { PlatformName = source.PlatformName, AreaCount = options.Value.Areas.Count };
                report.Platforms.Add(platformStats);
                // per platform de RunPlatformAsync aanroepen zonder await, en toevoegen aan de Task list zodat ze gelijktijdig uitgevoerd worden
                tasks.Add(RunPlatformAsync(source, platformStats, runId, scrapingDate, ct));
            }
            // pas wanneer alle tasks in de lijst (alle platforms) klaar zijn met scrapen, wordt onderstaande code uitgevoerd
            await Task.WhenAll(tasks);

            // einde (totale) run: eindtijd en totale duur van de scraperun loggen.
            scrapeRunStopwatch.Stop();
            report.Duration = scrapeRunStopwatch.Elapsed;
            report.LogSummary(logger);
        }


        // helper method die de WriteBatchAsync methode aanroept om een batch op te slaan naar de database. de helper method geeft het aantal mislukte weggeschreven listings terug
        private async Task<int> SaveBatchAsync(WriteToDb dbWriter, IAccommodationSource source, DateOnly scrapingDate, List<ScrapedAccommodation> batch, PlatformRunStats platformStats, CancellationToken ct)
        {
            int failed = 0;
            await dbWriteLock.WaitAsync(ct);
            try
            {
                await dbWriter.WriteBatchAsync(source.PlatformId, scrapingDate, batch, platformStats, ct);
                platformStats.ProcessedListingsCount += batch.Count;
                if (batch.Count > 0)
                {
                    logger.LogInformation("{Platform}: {ProcessedListingsCount}/{UniqueListingsCount} listings processed.", source.PlatformName, platformStats.ProcessedListingsCount, platformStats.UniqueListingsCount);
                }
            }
            catch (Exception ex)
            {
                if (ScrapeErrorClassifier.IsDatabaseUnavailable(ex))
                {
                    throw;
                }
                logger.LogError(ex, "Batch of {Count} listings on {Platform} not saved: {Error}", batch.Count, source.PlatformName, ex.InnerException?.Message ?? ex.Message);
                failed = batch.Count;
            }
            finally
            {
                dbWriteLock.Release();
            }
            batch.Clear();
            return failed;
        }

        // methode die alles uitvoert wat een platform tijdens een scrape-run moet doen
        // aparte methode gemaakt zodat het toegevoegd kan worden aan een lijst van Tasks (voor concurrent scraping)
        private async Task RunPlatformAsync(IAccommodationSource source, PlatformRunStats platformStats, Guid runId, DateOnly scrapingDate, CancellationToken ct)
        {

            // stopwatch die de duration van elk individueel platform bijhoudt
            Stopwatch platformRunStopwatch = Stopwatch.StartNew();

            // platformnaam aan elke log tijdens de platform-run toevoegen
            using IDisposable? platformScope = logger.BeginScope(new Dictionary<string, object> { ["Platform"] = source.PlatformName });
            logger.LogInformation("{Platform}: scraping started ({Areas} area(s))", source.PlatformName, options.Value.Areas.Count);

            try
            {
                // WriteToDb en RegionDbService gebruiken de DbContext, die scoped is. De orchestrator wordt vanuit de singleton Worker aangeroepen,
                // daarom krijgt elk platform een eigen scope en daarmee een eigen DbContext, die na het platform weer wordt opgeruimd
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                WriteToDb dbWriter = scope.ServiceProvider.GetRequiredService<WriteToDb>();
                RegionDbService regionDbService = scope.ServiceProvider.GetRequiredService<RegionDbService>();

                // listings worden per groep opgeslagen, zodat niet elke listing een eigen databaseaanroep krijgt
                List<ScrapedAccommodation> batch = new List<ScrapedAccommodation>();

                // alle accommodaties van dit platform worden hierin opgeslagen, over alle gescrapete gebieden
                List<DiscoveredAccommodation> platformResults = new List<DiscoveredAccommodation>();

                // discovery per gebied starten (te scrapen gebieden per run worden geconfigureerd in appsettings.json)
                foreach (ScrapeArea area in options.Value.Areas)
                {
                    // method uit TileAlgorithm.cs aanroepen die de discovery (inclusief tegelsplitsing) van accommodaties uitvoert
                    var (accommodations, areaFailedTiles) = await tileAlgorithm.DiscoverAsync(source, area, source.MaxDepth, ct);
                    // resultaten per gebied worden toegevoegd aan de platformresults lijst
                    platformResults.AddRange(accommodations);
                    // aantal opgehaalde listings per scrape-gebied opslaan zodat het gelogd kan worden tijdens de platform-run
                    platformStats.ListingsPerArea.Add(area.Name + ": " + accommodations.Count);

                    foreach (Tile failedTile in areaFailedTiles)
                    {
                        platformStats.FailedTileNames.Add(area.Name + "/" + failedTile.Name);
                    }
                }

                // lijst van alle opgehaalde accommodatie-listings ontdubbelen en in een nieuwe gefilterde lijst zetten
                List<DiscoveredAccommodation> deduplicatedList = platformResults.DistinctBy(accommodation => accommodation.SourceId).ToList();

                // aantallen vastleggen voor de samenvatting aan het einde van de platform-run
                platformStats.FoundListingsCount = platformResults.Count;
                platformStats.UniqueListingsCount = deduplicatedList.Count;

                logger.LogInformation("{Platform}: discovery completed, {Found} found ({ListingsPerArea}), {Unique} unique",
                                        source.PlatformName, platformStats.FoundListingsCount, string.Join(", ", platformStats.ListingsPerArea), platformStats.UniqueListingsCount);

                // houdt de mislukte listings en cooldowns van dit platform bij ('new' i.p.v. een DI omdat de cooldown tellers per platform apart moeten blijven, geen shared state)
                CooldownTracker cooldown = new CooldownTracker(options.Value.Resilience, platformStats, logger);

                // detailoperaties per listing: een mislukte listing wordt overgeslagen, zodat de rest van de run doorgaat 
                foreach (DiscoveredAccommodation listing in deduplicatedList)
                {
                    // alleen listings in Nederland verwerken: Booking.com bevat per listing een landcode waarop gecontroleerd kan worden
                    if (listing.CountryCode != null)
                    {
                        if (!string.Equals(listing.CountryCode, "nl", StringComparison.OrdinalIgnoreCase))
                        {
                            platformStats.ForeignListingsCount += 1;
                            continue;
                        }
                    }
                    // airbnb wordt handmatig gecontroleerd waarbij de coordinaten opgezocht worden in de localityboundary referentietabel 
                    else
                    {
                        bool isDutchListing = await regionDbService.IsDutchListingAsync(listing.Latitude, listing.Longitude, ct);
                        if (!isDutchListing)
                        {
                            platformStats.ForeignListingsCount += 1;
                            continue;
                        }
                    }
                    try
                    {
                        // beschikbaarheid van de listing ophalen 
                        IReadOnlyList<Availability> availabilityDays = await source.GetAvailabilityAsync(listing, ct);

                        // detailoperaties van de listing uitvoeren om het datamodel te vullen per listing
                        AccommodationDetails details = await source.GetDetailsAsync(listing, availabilityDays, ct);

                        // alle gescrapete fields controleren of ze nog werken naar behoren
                        platformStats.CountRetrievedFields(listing, details, availabilityDays);
                        
                        // alle requests voor deze listing zijn gelukt. tellers voor de foutafhandeling resetten
                        cooldown.ResetCounters();

                        batch.Add(new ScrapedAccommodation { Accommodation = listing, Availability = availabilityDays, Details = details });

                        if (batch.Count == WriteToDb.BatchSize)
                        {
                            platformStats.FailedListingsCount += await SaveBatchAsync(dbWriter, source, scrapingDate, batch, platformStats, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ScrapeErrorClassifier.StopsPlatform(ex)) // bevat alle verwachte fouten waarbij een platform-run bewust gestopt moet worden 
                        {
                            throw; 
                        }
                        if (ScrapeErrorClassifier.IsExpectedListingError(ex)) // bevat een lijst van alle verwachte fouten waarbij geen stacktrace nodig is
                        {
                            logger.LogWarning("Listing {SourceId} on {Platform} skipped: {Error}", listing.SourceId, source.PlatformName, ex.Message);
                        }
                        else // onverwachte fouten waarbij de stacktrace handig is
                        {
                            logger.LogWarning(ex, "Listing {SourceId} on {Platform} skipped (unexpected error)", listing.SourceId, source.PlatformName);
                        }
                        platformStats.FailedListingsCount += 1;
                        
                        // mislukte listing registreren (na MaxConsecutiveFailures volgt een cooldown, en na MaxCooldowns stopt een platform-run)
                        await cooldown.RegisterFailureAsync(ct);
                    }
                }
                // laatste batch wegschrijven. het return type van SaveBatchAsync is het aantal gefaalde listings voor het platform waarvoor weggeschreven wordt
                platformStats.FailedListingsCount += await SaveBatchAsync(dbWriter, source, scrapingDate, batch, platformStats, ct);

            }
            catch (BrokenCircuitException)  // loggen wanneer circuit geopent is (run gestopt for specifiek platform)
            {
                platformStats.StopReason = "Circuit open (Polly resilience)";
                logger.LogError("Scrape-run {RunId}: circuit opened for {Platform}, platform stopped for this run", runId, source.PlatformName);
            }
            catch (Exception ex)
            {
                platformStats.StopReason = ex.GetType().Name; // ex.Message kan ook als de error logging concreter moet

                // verwachte fouten worden opgevangen in ScrapeErrorClassifier, geen stacktrace nodig
                if (ScrapeErrorClassifier.IsExpectedPlatformError(ex))
                {
                    logger.LogError("Scrape-run {RunId}: {Platform} stopped: {Error}", runId, source.PlatformName, ex.Message);
                }
                else
                {
                    logger.LogError(ex, "Scrape-run {RunId}: unexpected error on {Platform}", runId, source.PlatformName);
                }
            }
            // einde platform run
            platformRunStopwatch.Stop();
            platformStats.PlatformRunDuration = platformRunStopwatch.Elapsed;
            platformStats.CheckFieldFillRates(logger, source.MonitoredFields);
            platformStats.LogSummary(logger, scrapingDate);

        }
    }
}
    

