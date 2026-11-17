using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Toeristenscraper.Scraper.SourceAbstraction.Booking.HttpServices
{
    public class AvailabilityCalendar(HttpClient httpClient) : GraphQLHttpService(httpClient, "dml/graphql")
    {
        public const int maxAmountOfDays = 61; // 61 beschikbaarheidsdatums in totaal per request
        private static readonly string BodyTemplate = LoadTemplate("Booking", "AvailabilityCalendar.json");

        public async Task<JsonDocument> GetPageAsync(string countryCode, string pageName, DateOnly startDate, CancellationToken ct)
        {
            string body = BodyTemplate
                .Replace("{{countryCode}}", countryCode)
                .Replace("{{pageName}}", pageName)
                .Replace("{{startDate}}", startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            return await PostAsync(body, ct);
        }
    }
}
