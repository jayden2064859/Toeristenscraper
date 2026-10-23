using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.SourceAbstraction.Booking.Operations
{
    public class AvailabilityCalendar(HttpClient httpClient)
    {
        public const int maxAmountOfDays = 61; // 61 beschikbaarheidsdatums in totaal per request
        public const int latestAllowedStartDate = 500; // de startdatum van een request mag 500 dagen vooruit zijn.

        private static readonly string BodyTemplate = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "SourceAbstraction", "Booking", "Queries", "AvailabilityCalendar.json"));

        public async Task<JsonDocument> GetPageAsync(string countryCode, string pageName, DateOnly startDate, CancellationToken ct)
        {
            string body = BodyTemplate
                .Replace("{{countryCode}}", countryCode)
                .Replace("{{pageName}}", pageName)
                .Replace("{{startDate}}", startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));


            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync("dml/graphql", content, ct);
            response.EnsureSuccessStatusCode(); 

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }
}
