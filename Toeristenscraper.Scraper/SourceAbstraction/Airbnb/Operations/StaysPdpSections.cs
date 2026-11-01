using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.Configuration;

namespace Toeristenscraper.Scraper.SourceAbstraction.Airbnb.Operations
{
    public class StaysPdpSections(HttpClient httpClient, IOptions<ScraperOptions> options) 
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
            response.EnsureSuccessStatusCode();

            // opgehaalde response returnen
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }
}
