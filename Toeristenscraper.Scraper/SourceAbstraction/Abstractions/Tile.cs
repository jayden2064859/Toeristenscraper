using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    public record Tile(
        double NeLat,
        double NeLng,
        double SwLat,
        double SwLng,
        string Name,
        int Depth);
}