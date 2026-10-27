using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // een accommodatie zoals gevonden door de zoekoperatie (FullSearch) 
    public class DiscoveredAccommodation
    {
        // alle velden die in de payload van de zoekoperatie worden teruggegeven 
        public required string SourceId { get; init; }
        public string? Title { get; init; }

        // lat en long worden in dit model als aparte properties behandeld, omdat ze als aparte fields opgehaald worden in Discovery. wordt later omgezet naar 1 'Coordinates' property
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public string? Address { get; init; } // straat + huisnummer (booking-only) 
        public string? AccommodationType { get; init; } 
        public decimal? StarClassification { get; init; } // booking-only
        public bool? Superhost { get; init; } // airbnb-only 
        public int? ReviewCount { get; init; }
        public decimal? AverageReviewScore { get; init; }
        public string? URL { get; init; } 
        public bool? IsExactLocation { get; init; }
        public string? CountryCode { get; init; }


        // platformspecifieke gegevens die alleen nodig zijn voor de detailoperaties van dat platform,
        // bijvoorbeeld pageName/countryCode (Booking.com) of federatedSearchId/photoId/categoryTag (Airbnb).
        // alleen het eigen platform leest deze uit.
        public IReadOnlyDictionary<string, string> DetailParameters { get; init; } = new Dictionary<string, string>();

    }
}