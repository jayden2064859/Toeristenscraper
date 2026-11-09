using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Transport
{
    // class met een methode om geldige session cookies op te halen voor Booking.com (HTML-pagina vereist een geldige cookie, anders wordt een challenge-pagina getoond)
    // uit testen op Postman blijkt dat alleen de aws-waf-token nodig is als cookie header
    public class BookingCookieProvider(ILogger<BookingCookieProvider> logger) 
    {
        public string? Cookie { get; private set; } = null;
        public async Task GetCookieAsync(string url)
        {
            try
            {
                using IPlaywright playwright = await Playwright.CreateAsync();
                await using IBrowser browser = await playwright.Chromium.LaunchAsync();
                IPage page = await browser.NewPageAsync();

                await page.GotoAsync(url);

                await page.WaitForSelectorAsync("script[data-capla-store-data='apollo']",
                    new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached, Timeout = 30000 });

                IReadOnlyList<BrowserContextCookiesResult> cookies = await page.Context.CookiesAsync();
                string? awsWafToken = cookies
                .FirstOrDefault(c => c.Name == "aws-waf-token")
                ?.Value;

                if (awsWafToken == null)
                {
                    throw new BlockedException("Booking.com: aws-waf-token not found after Playwright session");
                }
                Cookie = awsWafToken;
                logger.LogInformation("Booking.com: aws-waf-token refreshed");
            }
            catch (PlaywrightException ex)
            {
                throw new BlockedException("Booking.com: fetching aws-waf-token via Playwright failed: " + ex.Message);
            }

        }

    }
}
