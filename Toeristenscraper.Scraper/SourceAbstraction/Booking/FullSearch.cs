
using System.Globalization;
using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.SourceAbstraction.Booking
{
    // verstuurt de FullSearch-request voor één pagina van een tegel.
    // alleen request opbouwen en versturen. Het uitlezen van de response gebeurt in Parsing.
    public class FullSearch(HttpClient httpClient)
    {
        public const int PageSize = 100;    // maximaal aantal individuele listings dat Booking.com toestaat in één response-payload 
        public const int MaxResults = 1000; // maximaal aantal individuele listings dat Booking.com toestaat voor de gehele resultatenset. (om de volledige 1000 op te halen moet offset 0 t/m 900 gebruikt worden, incrementeel met +100)

        private static readonly string BodyTemplate = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "SourceAbstraction", "Booking", "Queries", "FullSearch.json"));

        public async Task<JsonDocument> GetPageAsync(Tile tile, int offset, CancellationToken ct)
        {
            string body = BodyTemplate
                .Replace("{{neLat}}", tile.NeLat.ToString(CultureInfo.InvariantCulture))
                .Replace("{{neLon}}", tile.NeLng.ToString(CultureInfo.InvariantCulture))
                .Replace("{{swLat}}", tile.SwLat.ToString(CultureInfo.InvariantCulture))
                .Replace("{{swLon}}", tile.SwLng.ToString(CultureInfo.InvariantCulture))
                .Replace("{{offset}}", offset.ToString(CultureInfo.InvariantCulture));

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync("dml/graphql", content, ct);
            response.EnsureSuccessStatusCode(); // Polly-strategies toevoegen

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }
}