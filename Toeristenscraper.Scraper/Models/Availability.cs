using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.Models
{
    // class die geinstantieerd wordt voor elke beschikbaarheidsdag in de AvailabilityCalendar API-operaties voor beide platforms
    public class Availability
    {
        public bool? IsAvailable { get; set; }
        public DateOnly? AvailabilityDate { get; set; }
        public int? MinLengthOfStay { get; set; }
        public int? MaxLengthOfStay { get; set; }

    }
}
