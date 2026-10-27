# Toeristenscraper
Scraper die dagelijks accommodatiegegevens van Booking.com en Airbnb verzamelt en omzet in marktinzichten.

## Projecten
- **Toeristenscraper.Data**: class library met de entiteiten, de DbContext en de migraties.
- **Toeristenscraper.Scraper**: worker service waarin het scraper-component wordt uitgewerkt.
- **Toeristenscraper.Web**: Razor Pages-project voor de presentatielaag (marktinzichten met filtermogelijkheden). Nog niet uitgewerkt.

## Werking van de scraper
Een scrape-run start in de **Worker**, die de **ScrapeOrchestrator** aanroept. De orchestrator loopt over alle platforms en alle gebieden uit appsettings.json, verzamelt de resultaten en ontdubbelt ze daarna met **DistinctBy()**.

Per gebied voert het **TileAlgorithm** de bounding-box-strategie uit. Het hele gebied wordt eerst als één tegel doorzocht. Bereikt een tegel de resultatenlimiet, dan wordt hij in vier kwadranten gesplitst, tot de ingestelde MaxDepth (elk platform heeft een eigen configuratie voor MaxDepth). Bereikt een tegel op MaxDepth nog steeds de limiet, dan wordt de tegel niet verder doorzocht en wordt er een waarschuwing gelogd.

Het doorzoeken van een tegel gebeurt via **IAccommodationSource**, met per platform een eigen implementatie. Voor Booking.com is dat **BookingSource**, die via **FullSearch** de requests verstuurt en de responses laat omzetten door **FullSearchParser**. Hierdoor bevatten de orchestrator en het tegelalgoritme geen platformspecifieke code.

Listings met een landcode buiten Nederland worden na de discovery overgeslagen. Listings zonder landcode (Airbnb) worden wel verwerkt.

## Availability - Booking.com
Voor elke opgehaalde listing in een FullSearch response worden 366 beschikbaarheidsdagen opgehaald, verdeeld over requests van 61 dagen (AvailabilityCalendar).

## Opslag
De gevonden listings worden per batch van 100 weggeschreven door **WriteToDb**, met één SaveChanges per batch (EF Core).
- **Versionering**: een listing wordt nooit bijgewerkt. Alleen als een inhoudelijk veld gewijzigd is ten opzichte van de laatst opgeslagen versie, wordt een nieuwe rij vastgelegd.
- **Reviews**: een nieuwe rij alleen als het aantal reviews of de gemiddelde score gewijzigd is.
- **Beschikbaarheid**: wordt bij elke scrape opgeslagen, gekoppeld aan de meest recente versie van de listing. De tabel is een TimescaleDB-hypertable op ScrapingDate. Een compressiebeleid comprimeert elke scrapedag zodra deze ouder is dan één dag.
- **Locatie**: woonplaats, gemeente en provincie worden bij het opslaan bepaald uit de coördinaten, met een ruimtelijke query (PostGIS) op de referentietabel `ref_locality_boundary` (zie Lokaal draaien).

Een fout in één batch slaat alleen die batch over. Is de database onbereikbaar, dan stopt het scrapen voor het platform.

## Mappen in Toeristenscraper.Scraper
- **Configurations**: ScraperOptions, gekoppeld aan de sectie "Scraper" in appsettings.json.
- **Orchestration**: de ScrapeOrchestrator.
- **SourceAbstraction/Abstractions**: platformonafhankelijke contracten en modellen.
- **SourceAbstraction/Discovery**: het tegelalgoritme en de ontdubbeling.
- **SourceAbstraction/Booking**: de Booking.com-implementatie, inclusief de querytemplate.
- **Parsing/Booking**: de parser van de FullSearch-response.
- **Storage**: WriteToDb, het wegschrijven naar de database inclusief de locatiebepaling.
- **Transport**: de delay handler (wachttijd tussen requests) en de Polly-configuratie.

## Lokaal draaien
1. Maak in de root een `.env` aan met `POSTGRES_PASSWORD=...` en start de database met `docker compose up -d`.
2. Zet de connection string `ConnectionStrings:ToeristenDb` in de user secrets van het Scraper-project.
3. Pas de migraties toe met `dotnet ef database update`, of in de Package Manager Console met `Update-Database -StartupProject Toeristenscraper.Scraper`. De migraties maken ook de extensies PostGIS en TimescaleDB aan.
4. Importeer de referentietabel met woonplaatsgrenzen (zie hieronder).
5. Start de scraper.

### Referentietabel woonplaatsgrenzen
De tabel `ref_locality_boundary` wordt niet via de migraties aangemaakt (`ExcludeFromMigrations`), maar eenmalig geïmporteerd uit de BAG-woonplaatsen (PDOK) met ogr2ogr in Docker. Dit moet na stap 3, omdat PostGIS dan al aanwezig is. Vervang `WACHTWOORD` door het wachtwoord uit `.env`:

```powershell
docker run --rm ghcr.io/osgeo/gdal:ubuntu-small-latest ogr2ogr -f PostgreSQL "PG:host=host.docker.internal port=5433 dbname=toeristenscraper user=postgres password=WACHTWOORD" "OAPIF:https://api.pdok.nl/kadaster/bag/ogc/v2" -dialect OGRSQL -sql "SELECT woonplaats AS city, bronhouder_naam AS municipality, provincie_naam AS province FROM woonplaats WHERE status = 'Woonplaats aangewezen'" -nln ref_locality_boundary -t_srs EPSG:4326 -nlt MULTIPOLYGON -lco GEOMETRY_NAME=geom -overwrite
```

Koppeling met de BAG-velden: `woonplaats` -> city, `bronhouder_naam` -> municipality, `provincie_naam` -> province. Na de import bevat de tabel ongeveer 2500 woonplaatsen. 
