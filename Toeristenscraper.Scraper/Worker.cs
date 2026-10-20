using Microsoft.Extensions.Options;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Orchestration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Discovery;

namespace Toeristenscraper.Scraper
{
    // bepaalt wanneer een run wordt gestart

    // momenteel (tijdens de ontwikkelfase) is er nog geen scheduler geimplementeerd, en start een scrape-run pas zodra handmatig het project gestart wordt.
    // later komt hier een scheduler die de scrape-runs automatiseert (met de specifieke configuraties in appsettings)
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

