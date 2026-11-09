using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // wordt teruggegeven wanneer het aantal opeenvolgende listings die mislukken (alle soorten requests) even groot is als het geconfigureerde max aantal
    public class MaxConsecutiveFailuresException(string message) : Exception(message)
    {
    }
}

