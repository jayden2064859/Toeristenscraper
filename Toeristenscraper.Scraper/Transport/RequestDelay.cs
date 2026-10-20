using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;
using Toeristenscraper.Scraper.Configuration;

namespace Toeristenscraper.Scraper.Transport
{
    // klasse erft over van DelegatingHandler, een klasse die de pipeline van HTTP-requests kan aanpassen
    public class BookingRequestDelayHandler(IOptions<ScraperOptions> options) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // eerst wachten
            await Task.Delay(options.Value.Booking.RequestDelayMs, cancellationToken); // request-delay configuratie staat in appsettings.json

            // daarna de request echt versturen
            return await base.SendAsync(request, cancellationToken);
        }
    }

    public class AirbnbRequestDelayHandler(IOptions<ScraperOptions> options) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // eerst wachten
            await Task.Delay(options.Value.Airbnb.RequestDelayMs, cancellationToken);

            // daarna de request echt versturen
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
