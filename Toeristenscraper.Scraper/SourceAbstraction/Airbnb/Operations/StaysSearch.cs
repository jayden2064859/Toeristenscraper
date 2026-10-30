using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.SourceAbstraction.Airbnb.Operations
{
    public class StaysSearch(HttpClient httpClient)
    {
        public const int PageSize = 18; // max 18 listings per response payload
        public const int MaxPageCursors = 15; // max 15 page cursors per response (15x18 listings)

        private static readonly string BodyTemplate = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "SourceAbstraction", "Airbnb", "Queries", "StaysSearch.json"));

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

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync("api/v3", content, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }
}
