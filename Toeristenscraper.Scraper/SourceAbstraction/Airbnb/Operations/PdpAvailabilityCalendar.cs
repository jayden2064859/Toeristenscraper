using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Toeristenscraper.Scraper.SourceAbstraction.Airbnb.Operations
{
    public class PdpAvailabilityCalendar(HttpClient httpClient)
    {
        public const int maxAmountOfMonths = 12; // maximaal 12 kalendermaanden toegestaan 
        private static readonly string BodyTemplate = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "SourceAbstraction", "Airbnb", "Queries", "PdpAvailabilityCalendar.json"));

        public async Task<JsonDocument> GetPageAsync(string sourceId, int month, int year, CancellationToken ct)
        {
            string body = BodyTemplate
                .Replace("{{count}}", maxAmountOfMonths.ToString())
                .Replace("{{sourceId}}", sourceId)
                .Replace("{{month}}", month.ToString())
                .Replace("{{year}}", year.ToString());


            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync("api/v3", content, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }
}
