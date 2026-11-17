using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.Models
{
    public record Tile(
        double NeLat,
        double NeLng,
        double SwLat,
        double SwLng,
        string Name,
        int Depth,
        int Attempts = 0); // parameter om bij te houden hoe vaak er een request voor een tegel opnieuw is uitgevoerd
}