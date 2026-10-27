
using Polly.CircuitBreaker;
using System.Net;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;



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

    
    public class TileAlgorithm(ILogger<TileAlgorithm> logger) 
    {
        public async Task<(IReadOnlyList<DiscoveredAccommodation>, List<Tile>)> DiscoverAsync(IAccommodationSource source, ScrapeArea area, int maxDepth, CancellationToken ct)
        {
            List<DiscoveredAccommodation> foundAccommodations = new List<DiscoveredAccommodation>();

            // queue starten voor individuele tiles die gescraped moeten worden (aantal tiles is afhankelijk van de response van de vorige payload)
            Queue<Tile> queue = new Queue<Tile>();

            // eerste tile die doorzocht wordt is de volledige bounding-box, ongesplitst
            queue.Enqueue(new Tile(area.NeLat, area.NeLng, area.SwLat, area.SwLng, "T", 0));
            var failedTiles = new List<Tile>();

            while (queue.Count > 0)
            {
                Tile tile = queue.Dequeue();
                TileSearchResult result;
                try
                {
                    result = await source.SearchTileAsync(tile, ct);
                }
                catch (BrokenCircuitException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (ex is HttpRequestException httpException && httpException.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw;
                    }

                    logger.LogWarning(ex, "Tile {Area}/{TileName} (depth {Depth}) on {Platform} skipped", area.Name, tile.Name, tile.Depth, source.PlatformName);
                    failedTiles.Add(tile);
                    continue;   // door met de volgende tegel in de queue
                }



                // loggen op welk tegelniveau momenteel gescraped wordt
                logger.LogDebug("Tile {TileName} (depth {Depth}): limit reached = {LimitReached}",
                tile.Name, tile.Depth, result.LimitReached);

                if (result.LimitReached == false)
                {
                    // lijst van accommodaties (parsed data voor het datamodel) teruggeven wanneer een response <1000 accommodatie-listings bevat
                    foundAccommodations.AddRange(result.Accommodations);
                }
                else if (result.LimitReached == true && tile.Depth < maxDepth)
                {
                    // tegel opsplitsen en de vier kwadranten achteraan in de wachtrij zetten
                    IReadOnlyList<Tile> quadrants = SplitTile(tile);
                    foreach (var quadrant in quadrants)
                    {
                        queue.Enqueue(quadrant);
                    }
                }
                else // wanneer een tegel nog steeds de resultatenlimiet bereikt en op maximale depth zit. 
                {
                    // opgehaalde batches opslaan met een warning log
                    foundAccommodations.AddRange(result.Accommodations);
                    logger.LogWarning("Tile {Area}/{TileName} at max depth ({MaxDepth}) still exceeding total results limit", area.Name, tile.Name, tile.Depth);
                }
            }

            return (foundAccommodations, failedTiles);
        }


        // methode voor het splitsen van een tegel in vier kwadranten
        public static IReadOnlyList<Tile> SplitTile(Tile tile)
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

