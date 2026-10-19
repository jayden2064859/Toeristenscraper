using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Data.Entities
{
    public class AccommodationReview
    {
        public int AccommodationReviewId { get; set; }
        public int AccommodationListingId { get; set; } // fk 
        public int? ReviewCount { get; set; }
        public decimal? AverageReviewScore { get; set; }
        public DateOnly ScrapingDate { get; set; }

        // navigation property
        public AccommodationListing AccommodationListing { get; set; } = null!;
    }
}
