using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.Models;

namespace Toeristenscraper.Scraper.SourceAbstraction.Airbnb.HttpServices
{
    public class StaysSearch(HttpClient httpClient) : GraphQLHttpService(httpClient, "api/v3")
    {
        public const int PageSize = 18; // max 18 listings per response payload
        public const int MaxPageCursors = 15; // max 15 page cursors per response (15x18 listings)

        private static readonly string BodyTemplate = LoadTemplate("AIrbnb", "StaysSearch.json");

        public async Task<JsonDocument> GetPageAsync(Tile tile, string? cursor, CancellationToken ct)
        {
            // eerste StaysSearch pagina heeft 'cursor: null' als waarde. pagina 2 en verder gebruiken een base64 encoded string
            string cursorJson = "null";
            if (cursor != null)
            {
                cursorJson = JsonSerializer.Serialize(cursor);
            }

            string body = BodyTemplate
                .Replace("{{neLat}}", tile.NeLat.ToString(CultureInfo.InvariantCulture))
                .Replace("{{neLng}}", tile.NeLng.ToString(CultureInfo.InvariantCulture))
                .Replace("{{swLat}}", tile.SwLat.ToString(CultureInfo.InvariantCulture))
                .Replace("{{swLng}}", tile.SwLng.ToString(CultureInfo.InvariantCulture))
                .Replace("{{cursor}}", cursorJson);

            return await PostAsync(body, ct);
        }
    }
}
