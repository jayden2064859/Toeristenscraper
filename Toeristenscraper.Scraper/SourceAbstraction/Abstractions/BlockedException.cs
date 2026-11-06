using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // wordt teruggegeven wanneer een GET-request voor Booking.com's HTML pagina een challenge-pagina teruggeeft, zelfs na het ophalen en vernieuwen van sessiecookies
    public class BlockedException(string message) : Exception(message)
    {
    }
}
