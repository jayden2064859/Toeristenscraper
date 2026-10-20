using Microsoft.Extensions.Options;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Orchestration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Discovery;

namespace Toeristenscraper.Scraper
{
    // bepaalt wanneer een run wordt gestart
    public class Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // per run een nieuwe scope, zodat de orchestrator (en straks de DbContext) steeds een nieuwe instantie is
            using IServiceScope scope = scopeFactory.CreateScope();
            ScrapeOrchestrator orchestrator = scope.ServiceProvider.GetRequiredService<ScrapeOrchestrator>();

            await orchestrator.RunAsync(stoppingToken);
        }
    }
}

