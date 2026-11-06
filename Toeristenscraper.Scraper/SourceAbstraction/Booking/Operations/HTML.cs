using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.Transport;

namespace Toeristenscraper.Scraper.SourceAbstraction.Booking.Operations
{
    // HTML van accommodatiepagina's worden opgehaald om voorzieningen en kamergerelateerde data per listing op te halen
    public class HTML(HttpClient httpClient, IOptions<ScraperOptions> options, BookingCookieProvider cookieProvider, ILogger<HTML> logger)
    {
        public async Task<JsonDocument> GetPageAsync(string pageName, DateOnly checkIn, DateOnly checkOut, CancellationToken ct)
        {
            // leeftijden van kinderen worden niet in het datamodel opgeslagen. Daarnaast is het uitwerken van zoekparameters incl kinderen geen eis van de productowner,
            // dus de keuze is gemaakt om dit niet uit te werken, zodat er geen data zonder herleidbaarheid in de database terecht kan komen
            if (options.Value.SearchParameters.Children > 0)
            {
                throw new NotSupportedException("Booking.com: searching with children is not supported");
            }

            string url = options.Value.Booking.BaseUrl + "hotel/nl/" + pageName + ".nl.html"
            + "?checkin=" + checkIn.ToString("yyyy-MM-dd")
            + "&checkout=" + checkOut.ToString("yyyy-MM-dd")
            + "&group_adults=" + options.Value.SearchParameters.Adults
            + "&group_children=" + options.Value.SearchParameters.Children
            + "&no_rooms=1"
            + "&lang=nl";

            if (options.Value.SearchParameters.Pets > 0)
            {
                url += "&travelling_with_pets=1"; // huisdieren werken in de zoekopdracht niet als numeriek aantal, maar als true (1) of false (0)
            }

            string? html = await SendAsync(url, ct);

            // challenge-pagina (HTTP 202): token ontbreekt (eerste request) of is verlopen
            if (html == null)
            {
                await cookieProvider.GetCookieAsync(url);
                html = await SendAsync(url, ct);

                if (html == null)
                {
                    throw new BlockedException("Booking.com: challenge page (HTTP 202) returned after refreshing aws-waf-token");
                }
            }

            // het Apollo-script (JSON) uit de HTML halen, hierin staat alle data die geparsed wordt
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            HtmlNode? apolloNode = htmlDoc.DocumentNode.SelectSingleNode("//script[@data-capla-store-data='apollo']");
            if (apolloNode == null)
            {
                // geen challenge, maar ook geen Apollo-data: andere pagina of gewijzigde opbouw, alleen deze listing wordt overgeslagen
                throw new InvalidOperationException("Booking.com: Apollo data not found on property page " + pageName);
            }
            // booking.com negeert de zoekparameters als de request als robot wordt gezien, de pagina bevat dan geen prijzen
            if (apolloNode.InnerHtml.Contains("\\\"isRobot\\\":true"))
            {
                throw new BlockedException("Booking.com: request treated as robot, page contains no prices for " + pageName);
            }
            return JsonDocument.Parse(apolloNode.InnerHtml);
        }

        private async Task<string?> SendAsync(string url, CancellationToken ct)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", "aws-waf-token=" + cookieProvider.Cookie);

            using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            if (response.StatusCode == HttpStatusCode.Accepted) // statuscode 202 is een challenge-pagina
            {
                return null;
            }
            return await response.Content.ReadAsStringAsync(ct);
        }


    }
}