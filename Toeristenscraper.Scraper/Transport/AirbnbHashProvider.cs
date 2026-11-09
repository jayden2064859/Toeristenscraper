using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Transport
{
    // class met een methode die een nieuwe geldige persisted query hash ophaalt van Airbnb met een headless browser.
    // StaysPdpSections vereist een persisted query hash om alle benodigde data uit het datamodel terug te kunnen krijgen. Eens in de zoveel tijd verloopt deze hash, 
    // wanneer een ongeldige hash mee wordt gegeven in de request wordt error 400 teruggegeven met PersistedQueryNotFound
    public class AirbnbHashProvider(IOptions<ScraperOptions> options, ILogger<AirbnbHashProvider> logger)
    {
        // actuele hash (startwaarde uit appsettings), wordt vernieuwd met Playwright als hij verlopen is
        public string Hash { get; private set; } = options.Value.Airbnb.StaysPdpSectionsHash;

        public async Task RefreshAsync(string sourceId)
        {
            logger.LogWarning("StaysPdpSections: persisted query hash is outdated (HTTP 400 persisted_query_not_found), refreshing with Playwright");
            try
            {
                using IPlaywright playwright = await Playwright.CreateAsync();
                await using IBrowser browser = await playwright.Chromium.LaunchAsync();
                IPage page = await browser.NewPageAsync();

                // listingpagina openen en wachten op de StaysPdpSections-request die de website zelf verstuurt
                IRequest request = await page.RunAndWaitForRequestAsync(
                    () => page.GotoAsync(options.Value.Airbnb.BaseUrl + "rooms/" + sourceId),
                    request => request.Url.Contains("/api/v3/StaysPdpSections/"));

                // de hash is het laatste deel van het pad: /api/v3/StaysPdpSections/<hash>
                string newHash = new Uri(request.Url).Segments.Last();
                
                if (newHash == Hash)
                {
                    throw new OutdatedHashException("StaysPdpSections: Playwright found the same outdated hash, refresh failed");
                }        
                Hash = newHash;
                logger.LogWarning("StaysPdpSections: persisted query hash refreshed");
            }
            catch (PlaywrightException ex)
            {
                throw new OutdatedHashException("StaysPdpSections: refreshing persisted query hash via Playwright failed: " + ex.Message);
            }

        }
    }
}