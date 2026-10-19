using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Data.Entities
{
    public class AccommodationAmenity
    {
        public int AccommodationAmenityId { get; set; }
        public int AccommodationListingId { get; set; } // fk 
        public required string AmenityValue { get; set; }
        public DateOnly ScrapingDate { get; set; }


        // navigation properties
        public AccommodationListing AccommodationListing { get; set; } = null!;

    }
}
