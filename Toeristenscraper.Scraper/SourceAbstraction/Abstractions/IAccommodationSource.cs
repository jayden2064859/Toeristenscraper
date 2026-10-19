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
        // (1 = Booking.com, 2 = Airbnb)
        int PlatformId { get; }

        // doorzoekt één tegel. haalt eerst de eerste pagina op
        Task<TileSearchResult> SearchTileAsync(Tile tile, CancellationToken cancellationToken);

        // later toevoegen: detailoperaties per gevonden accommodatie.
    }
}