using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Analytics.Models
{
    // record klasse voor de herleidbaarheid van alle berekende inzichten 
    // filter: de gebruikte filters voor het inzicht
    // first- en lastScrapingDate: de range van datums binnen het berekende inzicht
    // calculatedAt: wanneer het inzicht berekend is
    // applicationVersion: de versie van de applicatie waarop het inzicht berekend is (Git versie)
    public record InsightMetadata(InsightFilter filter, DateOnly? firstScrapingDate, DateOnly? lastScrapingDate, DateTime calculatedAt, string applicationVersion);
}
