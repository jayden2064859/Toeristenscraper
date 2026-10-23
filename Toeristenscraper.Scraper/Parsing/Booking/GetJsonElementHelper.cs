using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Toeristenscraper.Scraper.Parsing.Booking
{
    public class GetJsonElementHelper
    {
        // helper method om een veld uit een JsonElement te halen, met pad als array van strings.
        public static JsonElement? Get(JsonElement element, params string[] path)
        {
            JsonElement current = element;


            foreach (string name in path)
            {
                if (current.ValueKind != JsonValueKind.Object) return null;

                // stoppen als het veld niet bestaat
                if (!current.TryGetProperty(name, out JsonElement next))
                {
                    return null;
                }

                // stoppen als het veld bestaat maar null is
                if (next.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                // een niveau dieper
                current = next;
            }

            return current;
        }
    }
}
