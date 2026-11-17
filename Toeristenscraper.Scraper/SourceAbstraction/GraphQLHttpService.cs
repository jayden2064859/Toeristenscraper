using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.Exceptions;
using Toeristenscraper.Scraper.Parsing.Shared;

namespace Toeristenscraper.Scraper.SourceAbstraction
{
    // basisklasse voor alle GraphQL-operaties van beide platforms.
    // de subklassen bouwen alleen hun eigen request-body op, het versturen en controleren van de response gebeurt hier
    public class GraphQLHttpService(HttpClient httpClient, string endpoint)
    {
        // laadt een querytemplate uit de Queries-map van een platform 
        protected static string LoadTemplate(string platform, string fileName)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SourceAbstraction", platform, "Queries", fileName));
        }

        // verstuurt de request en geeft de ruwe response terug, voor operaties die de statuscode zelf eerst willen controleren
        protected async Task<HttpResponseMessage> SendAsync(string body, CancellationToken ct)
        {
            StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
            return await httpClient.PostAsync(endpoint, content, ct);
        }

        // statuscode controleren, response uitlezen als JSON en controleren op GraphQL-fouten
        protected async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
        {
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
            JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // een GraphQL-response kan statuscode 200 hebben, maar toch een fout bevatten in het errors-veld
            if (doc.RootElement.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                string? message = GetJsonElementHelper.Get(errors[0], "message")?.GetString();
                doc.Dispose();
                throw new GraphQLErrorException(GetType().Name + ": response contains GraphQL error: " + message);
            }
            return doc;
        }

        // versturen en uitlezen
        protected async Task<JsonDocument> PostAsync(string body, CancellationToken ct)
        {
            using HttpResponseMessage response = await SendAsync(body, ct);
            return await ReadJsonAsync(response, ct);
        }

    }
}
