
namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // wordt gegooid wanneer de persisted query hash verlopen is en niet vernieuwd kon worden, alle volgende requests mislukken dan ook
    public class OutdatedHashException(string message) : Exception(message)
    {
    }
}