# Toeristenscraper
Scraper die dagelijks accommodatiegegevens van Booking.com en Airbnb verzamelt en omzet in marktinzichten.

## Projecten
- **Toeristenscraper.Data**: class library met de entiteiten, de DbContext en de migraties.
- **Toeristenscraper.Scraper**: worker service waarin het scraper-component wordt uitgewerkt.
- **Toeristenscraper.Web**: Razor Pages-project voor de presentatielaag (marktinzichten met filtermogelijkheden).

## Werking van de scraper
Een scrape-run start in de **Worker**, die de **ScrapeOrchestrator** aanroept. De orchestrator loopt over alle platforms en alle gebieden uit appsettings.json, verzamelt de resultaten en ontdubbelt ze daarna met **Deduplicate**.

Per gebied voert het **TileAlgorithm** de bounding-box-strategie uit. Het hele gebied wordt eerst als één tegel doorzocht. Bereikt een tegel de resultatenlimiet, dan wordt hij in vier kwadranten gesplitst, tot de ingestelde MaxDepth (elk platform heeft een eigen configuratie voor MaxDepth) 

Het doorzoeken van een tegel gebeurt via **IAccommodationSource**, met per platform een eigen implementatie. Voor Booking.com is dat **BookingSource**, die via **FullSearch** de requests verstuurt en de responses laat omzetten door **FullSearchParser**. Hierdoor bevatten de orchestrator en het tegelalgoritme geen platformspecifieke code.

## Availability - Booking.com
Voor elk opgehaalde listing in een FullSearch response worden 366 beschikbaarheidsdagen opgehaald.

## Mappen in Toeristenscraper.Scraper
- **Configurations**: ScraperOptions, gekoppeld aan de sectie "Scraper" in appsettings.json.
- **Orchestration**: de ScrapeOrchestrator.
- **SourceAbstraction/Abstractions**: platformonafhankelijke contracten en modellen.
- **SourceAbstraction/Discovery**: het tegelalgoritme en de ontdubbeling.
- **SourceAbstraction/Booking**: de Booking.com-implementatie, inclusief de querytemplate.
- **Parsing/Booking**: de parser van de FullSearch-response.
- **Transport**: de delay handler die een wachttijd tussen requests aanhoudt.

## Lokaal draaien
Maak in de root een `.env` aan met `POSTGRES_PASSWORD` en start de database met `docker compose up -d`. Zet de connection string `ConnectionStrings:ToeristenDb` in de user secrets van het Scraper-project, pas de migraties toe met `dotnet ef database update` en start daarna de scraper.
