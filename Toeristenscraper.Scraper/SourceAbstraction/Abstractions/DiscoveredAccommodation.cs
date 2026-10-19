using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // een accommodatie zoals gevonden door de zoekoperatie
    public class DiscoveredAccommodation
    {
        // alle velden die in de payload van de zoekoperatie worden teruggegeven 
        public required string SourceId { get; init; }
        public string? Title { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public string? Address { get; init; } // straat + huisnummer (booking-only) 
        public string? AccommodationType { get; init; } 
        public decimal? StarClassification { get; init; } // booking-only
        public bool? Superhost { get; init; } // airbnb-only 
        public int? ReviewCount { get; init; }
        public decimal? AverageReviewScore { get; init; }


        // platformspecifieke gegevens die alleen nodig zijn voor de detailoperaties van dat platform,
        // bijvoorbeeld pageName/countryCode (Booking.com) of federatedSearchId/photoId/categoryTag (Airbnb).
        // alleen het eigen platform leest deze uit.
        public IReadOnlyDictionary<string, string> DetailParameters { get; init; }
            = new Dictionary<string, string>();
    }
}