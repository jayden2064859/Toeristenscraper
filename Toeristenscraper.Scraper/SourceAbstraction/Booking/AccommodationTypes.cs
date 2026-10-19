using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Booking
{
    // booking.com toont intern alleen numerieke waarden per accommodatietype. Deze .cs file bevat de gevonden textuele mapping van elk type.
    public enum AccommodationTypes
    {
        Hostel = 203,
        Hotel = 204,
        Resort = 206,
        BnB = 208, // bed & breakfast
        Vakantiepark = 212,
        Villa = 213, 
        Camping = 214,
        Boot = 215,
        Pension = 216,
        Appartement = 219,
        Vakantiehuis = 220,
        Lodge = 221,
        Homestay = 222,
        Landhuis = 223,
        LuxeTent = 224,
        Chalet = 228
    }
}
