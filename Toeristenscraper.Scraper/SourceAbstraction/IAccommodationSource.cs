using Toeristenscraper.Scraper.Models;
using Toeristenscraper.Scraper.Reporting;

namespace Toeristenscraper.Scraper.SourceAbstraction
{
    // contract dat elk platform implementeert
    public interface IAccommodationSource
    {
        public string PlatformName { get; }
        public int PlatformId { get; }
        int MaxDepth { get; }
        IReadOnlyList<MonitoredField> MonitoredFields { get; } // lijst van fields die per platform gecontroleerd worden (controle of de fields nog daadwerkelijk waarden teruggeven)

        // discovery operaties: om accommodatie listings te ontdekken binnen een geconfigureerd zoekgebied
        Task<TileSearchResult> SearchTileAsync(Tile tile, CancellationToken cancellationToken);

        // detail operaties: om het datamodel van een listing te vullen met de details van een listing, inclusief beschikbaarheid per dag, kamers met voorzieningen en prijzen (op basis van zoekparameters)
        Task<IReadOnlyList<Availability>> GetAvailabilityAsync(DiscoveredAccommodation listing, CancellationToken ct);
        Task<AccommodationDetails> GetDetailsAsync(DiscoveredAccommodation listing, IReadOnlyList<Availability> availability, CancellationToken ct);

    }
}
