using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.Reporting
{
    // namen van de checks die na elke platform-run controleren of de parser nog data oplevert.
    // per listing wordt per check bijgehouden of er een waarde is (bij lijsten minstens één item)
    public enum MonitoredField
    {
        Title,
        AccommodationType,
        Coordinates,
        Availability, // minstens 1 beschikbaarheidsdag
        Rooms, // minstens 1 kamer
        TotalPrice, // minstens 1 kamer met een prijs
        Amenities, // minstens 1 voorziening
        ReviewCount,
        KvkNumber,
        RegistrationNumber,
        StarClassification, // wordt alleen gecontroleerd in BookingSource
        Superhost // wordt alleen gecontroleerd in AirbnbSource
    }
}
