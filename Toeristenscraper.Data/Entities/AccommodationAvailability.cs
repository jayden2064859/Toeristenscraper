using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Data.Entities
{
    public class AccommodationAvailability
    {
        public int AccommodationListingId { get; set; } // FK, samengestelde PK met AvailabilityDate en ScrapingDate
        public DateOnly AvailabilityDate { get; set; }
        public bool IsAvailable { get; set; }
        public int MinNights { get; set; } // minNights altijd aanwezig op beide platforms
        public int? MaxNights { get; set; } // maxNights alleen aanwezig op Airbnb 
        public DateOnly ScrapingDate { get; set; }

        // navigation property
        public AccommodationListing AccommodationListing { get; set; } = null!;
    }
}
