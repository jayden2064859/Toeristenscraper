using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // class met fields die in detailoperaties gevonden kunnen worden
    public class AccommodationDetails
    {
        public string? AccommodationType { get; init; }
        public bool? Superhost { get; init; }
        public int? ReviewCount { get; init; }
        public decimal? AverageReviewScore { get; init; }
        public bool? IsExactLocation { get; init; }
        public string? Address { get; init; }
        public string? KvkNumber { get; init; }
        public string? RegistrationNumber { get; init; }

        // zoekparameters waarvoor de prijzen gelden  worden bij elke kamer opgeslagen (voor herleidbaarheid)
        public DateOnly? CheckInDate { get; init; }
        public DateOnly? CheckOutDate { get; init; }
        public int? AdultCount { get; init; }
        public int? ChildCount { get; init; }
        public int? PetCount { get; init; }

        public IReadOnlyList<RoomDetails> Rooms { get; init; } = new List<RoomDetails>();
        public IReadOnlyList<string> Amenities { get; init; } = new List<string>(); // accommodatie-brede voorzieningen

    }
}
