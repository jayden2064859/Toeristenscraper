namespace Toeristenscraper.Scraper.Transport
{
    // klasse voor de request delay tussen requests, voor alle platforms (erft over van DelegatingHandler, een klasse die de pipeline van HTTP-requests kan aanpassen)
    public class RequestDelayHandler(int delayMs) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // eerst de ingestelde tijd wachten
            await Task.Delay(delayMs, cancellationToken); // request-delay configuratie staat in appsettings.json

            // daarna de request echt versturen
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
