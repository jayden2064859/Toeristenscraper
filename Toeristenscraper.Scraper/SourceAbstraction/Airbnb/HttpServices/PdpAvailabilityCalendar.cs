using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Toeristenscraper.Scraper.SourceAbstraction.Airbnb.HttpServices
{
    public class PdpAvailabilityCalendar(HttpClient httpClient) : GraphQLHttpService(httpClient, "api/v3")
    {
        public const int maxAmountOfMonths = 12; // maximaal 12 kalendermaanden toegestaan 
        private static readonly string BodyTemplate = LoadTemplate("Airbnb", "PdpAvailabilityCalendar.json");

        public async Task<JsonDocument> GetPageAsync(string sourceId, int month, int year, CancellationToken ct)
        {
            string body = BodyTemplate
                .Replace("{{count}}", maxAmountOfMonths.ToString())
                .Replace("{{sourceId}}", sourceId)
                .Replace("{{month}}", month.ToString())
                .Replace("{{year}}", year.ToString());

            return await PostAsync(body, ct);
        }
    }
}
