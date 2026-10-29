using Microsoft.Extensions.Options;
using Toeristenscraper.Scraper.Configurations;
using Toeristenscraper.Scraper.Orchestration;

namespace Toeristenscraper.Scraper
{
    // bepaalt wanneer een run wordt gestart
    public class Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IOptions<ScheduleOptions> options) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            if (options.Value.RunOnStartup == true)
            {
                await RunScrapeAsync(ct);
            }

            while (ct.IsCancellationRequested == false)
            {
                TimeSpan delay = GetDelayUntilNextRun();
                await Task.Delay(delay, ct);
                await RunScrapeAsync(ct);
            }


        }


        private async Task RunScrapeAsync(CancellationToken ct)
        {
            // per run een nieuwe scope, zodat de orchestrator (en straks de DbContext) steeds een nieuwe instantie is
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ScrapeOrchestrator orchestrator = scope.ServiceProvider.GetRequiredService<ScrapeOrchestrator>();

            try
            {
                await orchestrator.RunAsync(ct);
            }
            catch (OperationCanceledException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    return; // applicatie wordt afgesloten
                }
                logger.LogCritical(ex, "Scrape-run cancelled unexpectedly");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Scrape-run failed unexpectedly, next run will start at the scheduled time");
            }
        }

        // berekent hoe lang het nog duurt tot het eerstvolgende geconfigureerde tijdstip (RunTime in de tijdzone uit appsettings)
        private TimeSpan GetDelayUntilNextRun()
        {
            // timezone uit appsettings gebruiken
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
            DateTime now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, timeZone);
            DateTime nextRun = now.Date + options.Value.RunTime;

            if (nextRun <= now)
            {
                nextRun = nextRun.AddDays(1);
            }
            TimeSpan delay = nextRun - now;

            logger.LogInformation("Next scraperun starts at {NextRun}", nextRun);
            return delay;
        }
    }
}

