using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.WebRequestMethods;


namespace Toeristenscraper.Scraper.SourceAbstraction.Abstractions
{
    // contract dat elk platform implementeert
    // discovery en orchestration kennen alleen dit contract, nooit de platformspecifieke klassen
    public interface IAccommodationSource
    {
        public string PlatformName { get; }
        int MaxDepth { get; }

        // discovery operaties
        Task<TileSearchResult> SearchTileAsync(Tile tile, CancellationToken cancellationToken);

        // detail operaties
        Task<IReadOnlyList<Availability>> GetAvailabilityAsync(DiscoveredAccommodation listing, CancellationToken ct);

    }
}
