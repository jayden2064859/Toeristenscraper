using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    public class RoomDetails
    {
        public string? SourceRoomId { get; init; }
        public string? RoomType { get; init; }
        public decimal? RoomSize { get; init; } // in vierkante meter
        public int? MaxCapacity { get; init; }
        public int? BedCount { get; init; }
        public string? BedType { get; init; }
        public decimal? TotalPrice { get; init; }
        public bool? TaxIncluded { get; init; }
        public decimal? TaxAmount { get; init; }
        public IReadOnlyList<string> Amenities { get; init; } = new List<string>();
    }
}
