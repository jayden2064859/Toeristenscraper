# Toeristenscraper
Scraper die dagelijks accommodatiegegevens van Booking.com en Airbnb verzamelt en omzet in marktinzichten.

## Projecten
Toeristenscraper.Data is een class library met de entiteiten, de DbContext en de migraties. Toeristenscraper.Scraper is een worker service waarin het scraper-component is uitgewerkt. Toeristenscraper.Web is het Razor Pages-project voor de presentatielaag (nog niet uitgewerkt).

## Planning
De Worker start elke dag een scrape-run op het tijdstip uit de sectie Schedule in appsettings.json (RunTime, in de tijdzone TimeZone). De wachttijd tot de volgende run wordt telkens vanaf de klok berekend, zodat de starttijd niet verschuift. Met RunOnStartup start er ook direct een run bij het opstarten. In Development staat dit aan.

## Discovery
De Worker roept de ScrapeOrchestrator aan. De orchestrator loopt over alle platforms en alle gebieden uit appsettings.json, verzamelt de resultaten en ontdubbelt ze met DistinctBy().

Per gebied voert het TileAlgorithm de bounding-box-strategie uit. Het hele gebied wordt eerst als één tegel doorzocht. Bereikt een tegel de resultatenlimiet, dan wordt hij in vier kwadranten gesplitst, tot de MaxDepth van het platform. Bereikt een tegel op MaxDepth nog steeds de limiet, dan wordt hij niet verder doorzocht en wordt er een waarschuwing gelogd.

Het doorzoeken van een tegel gebeurt via IAccommodationSource, met een eigen implementatie per platform. Daardoor bevatten de orchestrator en het tegelalgoritme geen platformspecifieke code. BookingSource gebruikt FullSearch, met pagina's van 100 via een offset. De limiet is bereikt bij meer dan 1000 resultaten. AirbnbSource gebruikt StaysSearch zonder datums, met pagina's van 18 via paginacursors. De limiet is bereikt als de eerste pagina 15 paginacursors bevat.

Daarna worden alleen Nederlandse listings verder verwerkt. Booking.com geeft per listing een landcode terug. Bij Airbnb worden de coördinaten opgezocht in de referentietabel ref_locality_boundary. Ligt een listing in geen enkele Nederlandse woonplaats, dan wordt hij overgeslagen.

## Detailoperaties
Per listing worden de beschikbaarheid en de details opgehaald. Mislukt dit voor één listing, dan wordt in de meeste gevallen alleen die listing overgeslagen (zie Foutafhandeling).

Booking.com haalt de beschikbaarheid een jaar vooruit op met AvailabilityCalendar, in requests van 61 dagen. De details van Booking.com zijn nog niet uitgewerkt. GetDetailsAsync geeft daar voorlopig lege details terug.

Airbnb haalt de beschikbaarheid op met PdpAvailabilityCalendar: 12 kalendermaanden vanaf de huidige maand in één request. Dagen vóór de scrapedatum worden overgeslagen. De details komen uit StaysPdpSections (een persisted query): accommodatietype, exacte locatie, reviews, Superhost, KVK-nummer, registratienummer, voorzieningen, kamers en prijzen.

Een Airbnb-accommodatie met kamertypen, zoals een hotel, levert een kamer per kamertype op. Een accommodatie zonder kamertypen wordt als één kamer opgeslagen. Van de bedrijfsgegevens wordt alleen het KVK-nummer opgeslagen, geen naam of contactgegevens. Het registratienummer wordt zonder spaties en in hoofdletters opgeslagen, zodat hetzelfde nummer met of zonder spaties aan dezelfde vestiging wordt gekoppeld. Een waarde die niet het formaat NLD, gemeentecode (4 cijfers) en 16 hexadecimale tekens heeft, zoals "vrijstelling" of een voorbeeldnummer, wordt niet opgeslagen.

De totaalprijs is de regel "Totaal" uit de prijsopbouw, of anders de som van alle prijsregels. Een belastingbedrag wordt alleen opgeslagen bij een regel "Belastingen". Bij "Belastingen en toeslagen" wordt alleen vastgelegd dat er belasting is inbegrepen.

De parser verwacht Nederlandse teksten en m². De BaseUrl van Airbnb moet daarom https://www.airbnb.nl/ zijn.

## Zoekparameters voor prijzen
Prijzen horen bij een zoekopdracht. De zoekparameters staan in de sectie SearchParameters en zijn gelijk voor alle platforms, zodat prijzen vergelijkbaar zijn. De aankomstdatum is de scrapedatum plus CheckInOffsetDays. Het aantal nachten is Nights, of meer als de accommodatie op de aankomstdatum een langere minimale verblijfsduur heeft, omdat het platform anders geen prijs geeft. Het aantal gasten staat in Adults, Children en Pets.

Bij elke kamer worden de gebruikte zoekparameters opgeslagen (CheckInDate, CheckOutDate, AdultCount, ChildCount en PetCount). Omdat het aantal nachten per accommodatie kan verschillen, worden prijzen vergeleken als prijs per nacht. Is een accommodatie op de aankomstdatum niet beschikbaar, dan is er die dag geen prijs.

## Opslag
De listings worden per batch van 100 weggeschreven door WriteToDb, met één SaveChanges per batch.

Een listing wordt nooit bijgewerkt. Alleen als een inhoudelijk veld gewijzigd is ten opzichte van de laatst opgeslagen versie, wordt een nieuwe rij vastgelegd. Een review krijgt alleen een nieuwe rij als het aantal reviews of de gemiddelde score gewijzigd is. De beschikbaarheid wordt bij elke scrape opgeslagen, gekoppeld aan de meest recente versie van de listing. Kamers en de voorzieningen per kamer worden ook bij elke scrape opgeslagen, omdat de prijs hoort bij de zoekparameters van die scrape. De voorzieningen van de hele accommodatie worden alleen opnieuw vastgelegd als de set gewijzigd is. Listings met hetzelfde registratienummer worden aan dezelfde vestiging (AccommodationEstablishment) gekoppeld.

Hetzelfde gegeven kan uit de zoekresultaten en uit de details komen. De waarde uit de details gaat dan voor, die uit de zoekresultaten is de terugvalwaarde.

Het datamodel gaat uit van maximaal één scrape per listing per dag, omdat ScrapingDate een datum is. Bij een tweede run op dezelfde dag worden de beschikbaarheid, de kamers en de voorzieningen van al gescrapete listings overgeslagen.

De tabel met beschikbaarheid is een TimescaleDB-hypertable op ScrapingDate. Een compressiebeleid comprimeert elke scrapedag zodra deze ouder is dan één dag. Woonplaats, gemeente en provincie worden bij het opslaan bepaald uit de coördinaten, met een ruimtelijke query (PostGIS) op de referentietabel ref_locality_boundary.

## Foutafhandeling
Elke operatie heeft een eigen Polly-pipeline. Bij een 5xx-fout, een netwerkfout of een timeout wordt de request opnieuw geprobeerd. Bij een 429 (te veel requests) wordt eerst gewacht en dan opnieuw geprobeerd. Blijft het platform 429 teruggeven, dan opent de circuit breaker en stopt het scrapen voor dat platform. Bij een 404 stopt het platform ook, omdat de operatie dan waarschijnlijk niet meer bestaat.

Een mislukte tegel of listing wordt overgeslagen en gelogd, de rest van de run gaat door. Een fout in één batch slaat alleen die batch over. Bij een tijdelijke databasefout probeert EF Core het eerst opnieuw. Blijft de database onbereikbaar, dan stopt het scrapen voor het platform.

Aan het einde van elke run wordt de status gelogd (Succeeded, PartiallyFailed of Failed), met het aantal listings, de overgeslagen buitenlandse listings, de mislukte tegels, de mislukte listings en de gestopte platforms.

## Configuratie
Alle instellingen staan in appsettings.json. De sectie Scraper bevat de te doorzoeken gebieden (Areas, als bounding box), de zoekparameters (SearchParameters), de instellingen per platform (Booking en Airbnb: BaseUrl, MaxDepth en RequestDelayMs, en bij Airbnb ook de ApiKey en de StaysPdpSectionsHash) en de Polly-instellingen (Resilience). De sectie Schedule bevat de planning.

De connection string staat niet in appsettings.json, maar in de user secrets. De Airbnb-API-key en de hash zijn geen geheimen, omdat Airbnb ze naar elke browser stuurt.

## Mappen in Toeristenscraper.Scraper
Configurations bevat de option-klassen voor appsettings.json. Orchestration bevat de ScrapeOrchestrator. SourceAbstraction/Abstractions bevat de platformonafhankelijke contracten en modellen. SourceAbstraction/Discovery bevat het tegelalgoritme. SourceAbstraction/Booking en SourceAbstraction/Airbnb bevatten de implementaties per platform, met per operatie een klasse en een querytemplate. Parsing/Booking en Parsing/Airbnb bevatten de parsers. Storage bevat WriteToDb. Transport bevat de delay handler en de Polly-configuratie.

## Lokaal draaien
Maak in de root een .env aan met POSTGRES_PASSWORD=... en start de database met docker compose up -d. Zet daarna de connection string ConnectionStrings:ToeristenDb in de user secrets van het Scraper-project. Pas vanuit de root de migraties toe met dotnet ef database update --project Toeristenscraper.Data --startup-project Toeristenscraper.Scraper, of in de Package Manager Console met Update-Database -Project Toeristenscraper.Data -StartupProject Toeristenscraper.Scraper. De migraties staan in het Data-project, de connection string in het Scraper-project. De migraties maken ook de extensies PostGIS en TimescaleDB aan. Importeer daarna de referentietabel met woonplaatsgrenzen (zie hieronder) en start de scraper.

## Referentietabel woonplaatsgrenzen
De tabel ref_locality_boundary wordt niet via de migraties aangemaakt, maar eenmalig geïmporteerd uit de BAG-woonplaatsen (PDOK) met ogr2ogr in Docker. Dit moet na het toepassen van de migraties, omdat PostGIS dan al aanwezig is. Vervang WACHTWOORD door het wachtwoord uit .env:

```
docker run --rm ghcr.io/osgeo/gdal:ubuntu-small-latest ogr2ogr -f PostgreSQL "PG:host=host.docker.internal port=5433 dbname=toeristenscraper user=postgres password=WACHTWOORD" "OAPIF:https://api.pdok.nl/kadaster/bag/ogc/v2" -dialect OGRSQL -sql "SELECT woonplaats AS city, bronhouder_naam AS municipality, provincie_naam AS province FROM woonplaats WHERE status = 'Woonplaats aangewezen'" -nln ref_locality_boundary -t_srs EPSG:4326 -nlt MULTIPOLYGON -lco GEOMETRY_NAME=geom -overwrite
```

De BAG-velden worden zo gekoppeld: woonplaats wordt city, bronhouder_naam wordt municipality en provincie_naam wordt province. Na de import bevat de tabel ongeveer 2500 woonplaatsen. De import wordt bij voorkeur jaarlijks opnieuw uitgevoerd, omdat gemeentelijke herindelingen meestal op 1 januari ingaan.

## To do
- Detailoperaties van Booking.com (kamers, prijzen, KVK- en registratienummer)
- De persisted query hash van StaysPdpSections (Airbnb) automatisch vernieuwen via Playwright wanneer de huidige verlopen is
- Logging via Serilog naar Azure Blob Storage, uitgelezen door Wazuh, en een alert naar de systeembeheerder wanneer een platform stopt
- Unit- en integratietests (xUnit)
- Analyse-component
- Presentatielaag
