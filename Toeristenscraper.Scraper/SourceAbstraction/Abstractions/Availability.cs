using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    public class Availability
    {
        public bool? IsAvailable { get; set; }
        public DateOnly? AvailabilityDate { get; set; }
        public int? MinLengthOfStay { get; set; }
        public int? MaxLengthOfStay { get; set; }

    }
}
