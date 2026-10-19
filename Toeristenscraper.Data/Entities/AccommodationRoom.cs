using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Data.Entities
{
    public class AccommodationRoom
    {
        public int AccommodationRoomId { get; set; }
        public int AccommodationListingId { get; set; } // fk
        public string? SourceRoomId { get; set; } 
        public string? RoomType { get; set; }
        public decimal? RoomSize { get; set; } // airbnb geeft in ft2 terug in responses, moet omgezet worden naar m2
        public int? MaxCapacity { get; set; }
        public int? BedCount { get; set; }
        public string? BedType { get; set; }
        public decimal? TotalPrice { get; set; }
        public bool? TaxIncluded { get; set; }
        public decimal? TaxAmount { get; set; }
        public DateOnly CheckInDate { get; set; }
        public DateOnly CheckOutDate { get; set; }
        public int AdultCount { get; set; }
        public int ChildCount { get; set; }
        public int PetCount { get; set; }
        public DateOnly ScrapingDate { get; set; }

        // navigation properties
        public AccommodationListing AccommodationListing { get; set; } = null!;
        public ICollection<AccommodationRoomAmenity> AccommodationRoomAmenities { get; set; } = new List<AccommodationRoomAmenity>();






    }
}
