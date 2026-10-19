using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Data.Entities
{
    public class AccommodationRoomAmenity
    {
        public int AccommodationRoomAmenityId { get; set; }
        public int AccommodationRoomId { get; set; } // fk
        public required string AmenityValue { get; set; }
        public DateOnly ScrapingDate { get; set; }

        // navigation property
        public AccommodationRoom AccommodationRoom { get; set; } = null!;

    }
}
