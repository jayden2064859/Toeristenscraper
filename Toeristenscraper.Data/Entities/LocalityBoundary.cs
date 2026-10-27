using NetTopologySuite.Geometries;

namespace Toeristenscraper.Data.Entities
{
    // referentiedata: grenzen van woonplaatsen uit de BAG (PDOK)
    // wordt alleen gelezen, niet beheerd via migraties
    public class LocalityBoundary
    {
        public int Id { get; set; }
        public string? City { get; set; }
        public string? Municipality { get; set; }
        public string? Province { get; set; }
        public Geometry Geom { get; set; } = null!;
    }
}
