using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.GeometriesGraph;
using System;
using System.Collections.Generic;
using System.Text;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Booking;


namespace Toeristenscraper.Scraper.SourceAbstraction.Discovery
{
    // hier komt de logica van de bounding-box scrape-strategie: Zoekgebied definieren met neLat, neLon, swLat en swLon.
    // dit gehele gebied wordt verdeeld in tiles, die elk apart gescraped worden.

    // 1) Een bounding box wordt gedefinieerd met neLat, neLon, swLat en swLon.
    // 2) Deze bounding box van het gehele scrapegebied wordt als argument in de request van de API-operatie meegegeven. 
    // 3) Als de het resultatenlimiet bereikt is , wordt de bounding box verdeeld in 4 tiles (2x2) door het midden van de originele bounding box te berekenen.
    //    Dit wordt gedaan door het midden van de breedte- en lengtegraad van de originele bounding-box te berekenen
    // 4) Deze opgesplitste tiles worden vervolgens op dezelfde manier apart gescraped. Een tegel stopt met verder opsplitsen in een van deze 2 scenario's:
    //    a) het aantal resultaten is onder de max limiet, waardoor er geen reden is om verder op te splitsen, of
    //    b) maximaal ingestelde diepte van opsplitsing is bereikt (zodat een tegel niet oneindig op kan splitsen waardoor de scraper vastloopt)

    
    public class TileAlgorithm()
    {
        public async Task<List<DiscoveredAccommodation>> DiscoverAsync(
            IAccommodationSource source, ScrapeArea area, int maxDepth, CancellationToken ct)
        {
            List<DiscoveredAccommodation> foundAccommodations = new List<DiscoveredAccommodation>();

            // queue starten voor individuele tiles die gescraped moeten worden (aantal tiles is afhankelijk van de response van de vorige payload)
            Queue<Tile> queue = new Queue<Tile>();

            // eerste tile die doorzocht wordt is de volledige bounding-box, ongesplitst
            queue.Enqueue(new Tile(area.NeLat, area.NeLng, area.SwLat, area.SwLng, "T", 0));

            while (queue.Count > 0)
            {
                Tile tile = queue.Dequeue();
                TileSearchResult result = await source.SearchTileAsync(tile, ct);

                if (result.LimitReached == false)
                {
                    // lijst van accommodaties (parsed data voor het datamodel) teruggeven wanneer een response <1000 accommodatie-listings bevat
                    foundAccommodations.AddRange(result.Accommodations);
                }
                else if (result.LimitReached == true && tile.Depth < maxDepth)
                {
                    // tegel opsplitsen en de vier kwadranten achteraan in de wachtrij zetten
                    List<Tile> quadrants = SplitTile(tile);
                    foreach (var quadrant in quadrants)
                    {
                        queue.Enqueue(quadrant);
                    }
                }
            }

            return foundAccommodations;
        }


        // methode voor het splitsen van een tegel in vier kwadranten
        public static List<Tile> SplitTile(Tile tile)
        {
            double midLat = (tile.NeLat + tile.SwLat) / 2;
            double midLng = (tile.NeLng + tile.SwLng) / 2;

            // rechtsboven (noordoost)
            Tile quadrant1 = new Tile(
                tile.NeLat,
                tile.NeLng,
                midLat,
                midLng,
                tile.Name + "-NE",
                tile.Depth + 1);

            // linksboven (noordwest)
            Tile quadrant2 = new Tile(
                tile.NeLat,
                midLng,
                midLat,
                tile.SwLng,
                tile.Name + "-NW",
                tile.Depth + 1);

            // rechtsonder (zuidoost)
            Tile quadrant3 = new Tile(
                midLat,
                tile.NeLng,
                tile.SwLat,
                midLng,
                tile.Name + "-SE",
                tile.Depth + 1);

            // linksonder (zuidwest)
            Tile quadrant4 = new Tile(
                midLat,
                midLng,
                tile.SwLat,
                tile.SwLng,
                tile.Name + "-SW",
                tile.Depth + 1);

            return new List<Tile> { quadrant1, quadrant2, quadrant3, quadrant4 };
        }
    }

}

