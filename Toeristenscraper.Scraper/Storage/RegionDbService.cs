using Microsoft.EntityFrameworkCore;
using Toeristenscraper.Data;
using Toeristenscraper.Data.Entities;
using NetTopologySuite.Geometries;

namespace Toeristenscraper.Scraper.Storage
{
    // aparte service class die de controles m.b.t. gebiedsindeling (nederlandse listings filteren, regionale indeling) uitvoert
    public class RegionDbService(ToeristenscraperDbContext dbContext)
    {

        // methode voor airbnb om te checken of een listing binnen Nederland ligt 
        // hiervoor wordt de referentietabel LocalityBoundary gebruikt om de gebiedsindeling te bepalen voor elke listing 
        public async Task<bool> IsDutchListingAsync(double? latitude, double? longitude, CancellationToken ct)
        {
            // de referentietabel bevat alleen data over Nederland. Elke listing die coordinaten buiten NL bevat, wordt als null teruggegeven hier
            LocalityBoundary? region = await LookupRegionAsync(latitude, longitude, ct);
            return region != null;
        }


        // woonplaats, gemeente en provincie bepalen met PostGIS (ST_Contains via NetTopologySuite).
        // null wanneer er geen coördinaten zijn of het punt buiten Nederland ligt
        public async Task<LocalityBoundary?> LookupRegionAsync(double? latitude, double? longitude, CancellationToken ct)
        {
            if (latitude == null || longitude == null)
            {
                return null;
            }

            Point point = new Point(longitude.Value, latitude.Value) { SRID = 4326 };

            return await dbContext.LocalityBoundary
                .AsNoTracking() // ef core hoeft deze rijen niet bij te houden (read-only data)
                .Where(w => w.Geom.Contains(point))
                .FirstOrDefaultAsync(ct);
        }
    }
}


