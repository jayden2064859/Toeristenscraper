using System;
using System.Collections.Generic;
using System.Text;
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;

namespace Toeristenscraper.Data.Entities
{
    public class AccommodationListing
    { 
        public int AccommodationListingId { get; set; }
        public int? AccommodationEstablishmentId { get; set; } // fk 
        public int PlatformId { get; set; } // fk 
        public required string SourceId { get; set; } 
        public string? Title { get; set; }
        public string? KVKNumber { get; set; }  
        public string? AccommodationType { get; set; } // numeriek getal uit response payloads, handmatig gemapped naar typenamen
        public Point? Coordinates { get; set; } // postgis type 
        public bool? IsExactLocation { get; set; } 
        public string? City { get; set; }
        public string? Municipality { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public decimal? StarClassification { get; set; } // nog niet 100% zeker of het een double moet zijn
        public bool? Superhost { get; set; }
        public string? URL { get; set; }
        public DateOnly ScrapingDate { get; set; }


        // navigation properties
        public Platform Platform { get; set; } = null!; 
        public AccommodationEstablishment? AccommodationEstablishment { get; set; }

        public ICollection<AccommodationRoom> AccommodationRooms { get; set; } = new List<AccommodationRoom>();
        public ICollection<AccommodationAmenity> AccommodationAmenities { get; set; } = new List<AccommodationAmenity>();
        public ICollection<AccommodationReview> AccommodationReviews { get; set; } = new List<AccommodationReview>();

    }
}
