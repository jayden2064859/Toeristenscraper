using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Transport;

namespace Toeristenscraper.Scraper.SourceAbstraction.Airbnb.Operations
{
    public class StaysPdpSections(HttpClient httpClient, IOptions<ScraperOptions> options, AirbnbHashProvider hashProvider) 
    {
        private static readonly string BodyTemplate = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "SourceAbstraction", "Airbnb", "Queries", "StaysPdpSections.json"));
        public async Task<JsonDocument> GetPageAsync(string sourceId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct)
        {
            // verplichte graphql variabelen (request-body fields) die meegegeven moeten worden 
            // stayListingId en demandStayListingId zijn beide base64 van het sourceId met een eigen voorvoegsel
            string stayListingId = Convert.ToBase64String(Encoding.UTF8.GetBytes("StayListing:" + sourceId));
            string demandStayListingId = Convert.ToBase64String(Encoding.UTF8.GetBytes("DemandStayListing:" + sourceId));

            SearchParameterOptions search = options.Value.SearchParameters;

            // body aanmaken met alle benodigde request-body fields
            string body = BodyTemplate
                // sourceId variaties en persisted query hash
                .Replace("{{stayListingId}}", stayListingId)
                .Replace("{{demandStayListingId}}", demandStayListingId)
                .Replace("{{hash}}", options.Value.Airbnb.StaysPdpSectionsHash)

                // zoekparameters
                .Replace("{{checkIn}}", checkIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Replace("{{checkOut}}", checkOut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Replace("{{adultCount}}", search.Adults.ToString())
                .Replace("{{childrenCount}}", search.Children.ToString())
                .Replace("{{petCount}}", search.Pets.ToString());

            // request versturen
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync("api/v3", content, ct);

            // de api operatie geeft response 400 'persisted_query_not_found' terug wanneer de meegegeven hash in de request body verlopen is
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                string errorBody = await response.Content.ReadAsStringAsync(ct);
                if (errorBody.Contains("persisted_query_not_found") || errorBody.Contains("PersistedQueryNotFound"))
                {
                    await hashProvider.RefreshAsync(sourceId);
                    return await GetPageAsync(sourceId, checkIn, checkOut, ct);
                }
            }
            response.EnsureSuccessStatusCode();

            // opgehaalde response returnen
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }
}
