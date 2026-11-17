# Toeristenscraper
Scraper die dagelijks accommodatiegegevens van Booking.com en Airbnb verzamelt en omzet in marktinzichten.

## Projecten
- Toeristenscraper.Data is een class library met de entiteiten, de DbContext en de migraties.
- Toeristenscraper.Scraper is een worker service waarin het scraper-component is uitgewerkt.
- Toeristenscraper.Analytics is een tweede class library voor het uitlezen van de opgeslagen data, waarna inzichten berekend worden op basis van de meegegeven filter-input.
- Toeristenscraper.Web is het Razor Pages-project voor de presentatielaag.

## Mappen in Toeristenscraper.Scraper
Per map staat hieronder welke verantwoordelijkheid de klassen erin hebben. Een scrape-run begint in Hosting en loopt via Orchestration, Discovery, SourceAbstraction, Transport en Parsing naar Storage en Reporting.

- Configuration bevat de optionklassen voor appsettings.json. ScraperOptions.cs bevat alle klassen voor de sectie Scraper, ScheduleOptions.cs de klasse voor de sectie Schedule.
- Discovery bevat TileAlgorithm, de zoekstrategie met tegelsplitsing die voor alle platforms gelijk is.
- Exceptions bevat eigen exceptions voor verwachte fouten tijdens een scrape-run. Afhankelijk van de fout wordt een listing overgeslagen of stopt het platform.
- Hosting bevat Worker, de klasse die bepaalt wanneer er een scrape-run wordt gestart.
- Models bevat de platformonafhankelijke dataobjecten waarin de gescrapete data wordt bewaard tot deze in de database wordt opgeslagen.
- Orchestration bevat ScrapeOrchestrator, de klasse die bepaalt wat er tijdens een scrape-run gebeurt. ScrapeErrorClassifier bepaalt per soort fout of een listing wordt overgeslagen of het platform stopt. CooldownTracker houdt per platform de mislukte listings bij en bepaalt wanneer er een cooldown volgt.
- Parsing bevat per platform de parsers die de benodigde data uit een response halen om het datamodel te vullen. Parsing/Shared bevat een hulpklasse die door alle parsers wordt gebruikt.
- Reporting bevat de klassen die de statistieken van een platform en van de hele scrape-run bijhouden en aan het einde loggen, inclusief de controle op lege velden (MonitoredField).
- SourceAbstraction bevat IAccommodationSource, het contract dat elk platform implementeert, en GraphQLHttpService, de basisklasse voor alle GraphQL-requests. De mappen Booking en Airbnb bevatten de implementatie per platform, met in HttpServices per operatie een klasse en in Queries de bijbehorende querytemplates.
- Storage bevat WriteToDb, de klasse die een batch listings naar de database wegschrijft. RegionDbService bepaalt met PostGIS in welke woonplaats, gemeente en provincie een listing ligt. ListingMapper zet een gescrapete listing om naar een databaserecord en vergelijkt deze met de laatst opgeslagen versie.
- Transport bevat de klassen die ervoor zorgen dat requests geldig en betrouwbaar verstuurd worden. Dat zijn het vernieuwen van de persisted query hash voor Airbnb (AirbnbHashProvider), het vernieuwen van het aws-waf-token voor Booking.com (BookingCookieProvider), de vertraging tussen requests (RequestDelayHandler) en de Polly-pipeline voor retries, circuit breaker en timeout (ResiliencePipelineSetup).

## Lokaal draaien
### Database en migraties
Maak in de root een .env aan met POSTGRES_PASSWORD=... en start de database met docker compose up -d. Zet daarna de connection string ConnectionStrings:ToeristenDb in de user secrets van het Scraper-project. Pas vanuit de root de migraties toe met dotnet ef database update --project Toeristenscraper.Data --startup-project Toeristenscraper.Scraper, of in de Package Manager Console met Update-Database -Project Toeristenscraper.Data -StartupProject Toeristenscraper.Scraper. De migraties staan in het Data-project, de connection string in het Scraper-project. De migraties maken ook de extensies PostGIS en TimescaleDB aan. Importeer daarna de referentietabel met woonplaatsgrenzen (zie hieronder) en start de scraper.

### Referentietabel woonplaatsgrenzen
De tabel ref_locality_boundary wordt niet via de migraties aangemaakt, maar eenmalig geïmporteerd uit de BAG-woonplaatsen (PDOK) met ogr2ogr in Docker. Dit moet na het toepassen van de migraties, omdat PostGIS dan al aanwezig is. Vervang WACHTWOORD door het wachtwoord uit .env:

```
docker run --rm ghcr.io/osgeo/gdal:ubuntu-small-latest ogr2ogr -f PostgreSQL "PG:host=host.docker.internal port=5433 dbname=toeristenscraper user=postgres password=WACHTWOORD" "OAPIF:https://api.pdok.nl/kadaster/bag/ogc/v2" -dialect OGRSQL -sql "SELECT woonplaats AS city, bronhouder_naam AS municipality, provincie_naam AS province FROM woonplaats WHERE status = 'Woonplaats aangewezen'" -nln ref_locality_boundary -t_srs EPSG:4326 -nlt MULTIPOLYGON -lco GEOMETRY_NAME=geom -overwrite
```

De BAG-velden worden zo gekoppeld: woonplaats wordt city, bronhouder_naam wordt municipality en provincie_naam wordt province. Na de import bevat de tabel ongeveer 2500 woonplaatsen. De import wordt bij voorkeur jaarlijks opnieuw uitgevoerd, omdat gemeentelijke herindelingen meestal op 1 januari ingaan.

## Productie (Azure VM)
### Opzet
De scraper draait op een Azure VM met AlmaLinux. De database draait op dezelfde VM als Docker-container (toeristenscraper-db), met TimescaleDB en PostGIS. De broncode staat in /mnt2/toeristenscraper/src, de gepubliceerde applicatie in /mnt2/toeristenscraper/app.

De applicatie draait als systemd-service toeristenscraper. De service start automatisch bij het opstarten van de VM en wordt bij een crash opnieuw gestart (Restart=on-failure). De servicedefinitie staat in /etc/systemd/system/toeristenscraper.service en is te bekijken met systemctl cat toeristenscraper. Buiten een scrape-run wacht de service tot het volgende tijdstip uit de sectie Schedule.

In productie komen de secrets, zoals de connection string, uit Azure Key Vault. De VM haalt deze op met een managed identity, zodat er geen secrets op de VM of in de repository staan. De logs gaan naar journald op de VM en naar Azure Blob Storage, waar ze in Wazuh te bekijken zijn.

### Deployen
Een nieuwe versie wordt vanaf de master-branch gedeployd. Voer op de VM uit:

```
cd /mnt2/toeristenscraper/src
git pull
sudo systemctl stop toeristenscraper
dotnet publish Toeristenscraper.Scraper -c Release -o /mnt2/toeristenscraper/app
sudo systemctl start toeristenscraper
```

Publiceer altijd met -o naar /mnt2/toeristenscraper/app. Zonder -o komt de output in de bin-map van elk project terecht en blijft de service de oude versie draaien. Deploy niet tijdens een lopende scrape-run.

Controleer na het deployen of de querytemplates zijn meegekomen, of de service draait en of de nieuwe versie is gepubliceerd:

```
ls /mnt2/toeristenscraper/app/SourceAbstraction/*/Queries
systemctl status toeristenscraper
grep -a -o "1\.0\.0+[0-9a-f]*" /mnt2/toeristenscraper/app/Toeristenscraper.Scraper.dll | head -1
```

De laatste regel geeft de versie met de commit-hash. Dezelfde versie wordt aan het begin van elke scrape-run gelogd.

### Logs bekijken
De logs van de service zijn op te vragen met journalctl. Met het volgende commando worden de logs vanaf een bepaald moment in een tekstbestand gezet, dat daarna met WinSCP kan worden gedownload:

```
journalctl -u toeristenscraper --since "2026-11-17 03:00" --no-pager -o cat > ~/scrapelogs.txt
```

## Configuratie
Alle instellingen staan in appsettings.json. De sectie Scraper bevat de te doorzoeken gebieden (Areas, als bounding box), de zoekparameters (SearchParameters), de instellingen per platform (Booking en Airbnb: BaseUrl, MaxDepth en RequestDelayMs, en bij Airbnb ook de ApiKey en de StaysPdpSectionsHash) en de Polly-instellingen (Resilience). De sectie Schedule bevat de planning.

De connection string staat niet in appsettings.json. Lokaal staat deze in de user secrets, in productie in Azure Key Vault (zie Productie). De Airbnb-API-key en de hash zijn geen geheimen, omdat Airbnb ze naar elke browser stuurt.

## Werking van een scrape-run
### Planning
De Worker start elke dag een scrape-run op het tijdstip uit de sectie Schedule in appsettings.json (RunTime, in de tijdzone TimeZone). De wachttijd tot de volgende run wordt telkens vanaf de klok berekend, zodat de starttijd niet verschuift. Met RunOnStartup start er ook direct een run bij het opstarten. In Development staat dit aan.

### Discovery
De Worker roept de ScrapeOrchestrator aan. De orchestrator start per platform een eigen platform-run, en de platforms draaien gelijktijdig. Binnen een platform worden de gebieden uit appsettings.json na elkaar doorzocht. De resultaten van alle gebieden worden verzameld en met DistinctBy() ontdubbeld, omdat aangrenzende gebieden elkaar kunnen overlappen.

De gebieden zijn de 30 gemeenten met de hoogste opbrengst aan toeristenbelasting in 2025 (CBS, tabel 83643NED). Per gemeente is de bounding box bepaald met ST_Extent op de gemeentegrenzen in ref_locality_boundary. Een bounding box is rechthoekig, waardoor ook listings net buiten de gemeente gevonden worden. De woonplaats, gemeente en provincie van een listing worden daarom niet afgeleid uit het gebied, maar bij het opslaan bepaald uit de coördinaten (zie Opslag).

Per gebied voert het TileAlgorithm de bounding-box-strategie uit. Het hele gebied wordt eerst als één tegel doorzocht. Bereikt een tegel de resultatenlimiet, dan wordt hij in vier kwadranten gesplitst, tot de MaxDepth van het platform. Bereikt een tegel op MaxDepth nog steeds de limiet, dan wordt hij niet verder doorzocht en wordt er een waarschuwing gelogd.

Mislukt het doorzoeken van een tegel, dan wordt de tegel achteraan de wachtrij gezet en na 5 seconden één keer opnieuw geprobeerd. Een platform geeft soms bij dezelfde zoekopdracht kortstondig een fout terug, die bij een tweede poging wel slaagt. Mislukt de tegel ook dan, dan wordt hij overgeslagen en in de samenvatting van de run vermeld.

Het doorzoeken van een tegel gebeurt via IAccommodationSource, met een eigen implementatie per platform. Daardoor bevatten de orchestrator en het tegelalgoritme geen platformspecifieke code. BookingSource gebruikt FullSearch, met pagina's van 100 via een offset. De limiet is bereikt bij meer dan 1000 resultaten. AirbnbSource gebruikt StaysSearch zonder datums, met pagina's van 18 via paginacursors. De limiet is bereikt als de eerste pagina 15 paginacursors bevat.

Daarna worden alleen Nederlandse listings verder verwerkt. Booking.com geeft per listing een landcode terug. Bij Airbnb worden de coördinaten opgezocht in de referentietabel ref_locality_boundary met RegionDbService. Ligt een listing in geen enkele Nederlandse woonplaats, dan wordt hij overgeslagen. Bij een benaderde locatie kan deze controle afwijken (zie Beperkingen).

### Detailoperaties
Per listing worden de beschikbaarheid en de details opgehaald. Mislukt dit voor één listing, dan wordt in de meeste gevallen alleen die listing overgeslagen (zie Foutafhandeling).

Booking.com haalt de beschikbaarheid een jaar vooruit op met AvailabilityCalendar, in requests van 61 dagen. De details komen uit de Apollo-JSON in de HTML-pagina van de listing, met de zoekparameters als query string. Voor deze pagina is een aws-waf-token nodig. BookingCookieProvider haalt dit token op met Playwright en vernieuwt het bij een challenge (HTTP 202). Zonder browser-User-Agent behandelt Booking.com de request als robot en geeft het geen prijzen.

Per kamer worden de laagste totaalprijs (met de toeristenbelasting als TaxAmount), de bedden en de voorzieningen opgeslagen. Een kamer zonder aanbieding is niet beschikbaar en krijgt geen prijs. De voorzieningen van de hele accommodatie zijn bij Booking.com niet volledig (zie Beperkingen). Het KVK-nummer wordt alleen opgeslagen als het uit 8 cijfers bestaat.

Airbnb haalt de beschikbaarheid op met PdpAvailabilityCalendar: 12 kalendermaanden vanaf de huidige maand in één request. Dagen vóór de scrapedatum worden overgeslagen. De details komen uit StaysPdpSections (een persisted query): accommodatietype, exacte locatie, reviews, Superhost, KVK-nummer, registratienummer, voorzieningen, kamers en prijzen.

Een Airbnb-accommodatie met kamertypen, zoals een hotel, levert een kamer per kamertype op. Een accommodatie zonder kamertypen wordt als één kamer opgeslagen. Van de bedrijfsgegevens wordt alleen het KVK-nummer opgeslagen, geen naam of contactgegevens. Het registratienummer wordt zonder spaties en in hoofdletters opgeslagen, zodat hetzelfde nummer met of zonder spaties aan dezelfde vestiging wordt gekoppeld. Een waarde die niet het formaat NLD, gemeentecode (4 cijfers) en 16 hexadecimale tekens heeft, zoals "vrijstelling" of een voorbeeldnummer, wordt niet opgeslagen.

De totaalprijs is de regel "Totaal" uit de prijsopbouw, of anders de som van alle prijsregels. Een belastingbedrag wordt alleen opgeslagen bij een regel "Belastingen". Bij "Belastingen en toeslagen" wordt alleen vastgelegd dat er belasting is inbegrepen.

Alle GraphQL-requests lopen via GraphQLHttpService. Bevat een response een GraphQL-fout, dan wordt de eerste foutmelding gelogd en wordt de listing overgeslagen.

### Persisted query hash vernieuwen
StaysPdpSections (Airbnb) wordt aangeroepen met een persisted query: in plaats van de volledige querytekst stuurt de request een SHA-256-hash mee, waaronder de query op de server van Airbnb is geregistreerd. Een eigen querytekst werkt ook, maar geeft de prijs en de kamernaam niet terug. Daarom wordt de hash gebruikt. De startwaarde staat in appsettings.json (StaysPdpSectionsHash).

Airbnb wijzigt deze hash af en toe. Een verouderde hash geeft een HTTP 400 met persisted_query_not_found. StaysPdpSections herkent deze fout, waarna AirbnbHashProvider met Playwright (headless Chromium) de listingpagina opent. De website verstuurt daarbij zelf een StaysPdpSections-request, met de geldige hash als laatste deel van de URL. Die hash wordt overgenomen en in het geheugen bijgewerkt, en de request wordt één keer herhaald. Daarna gebruiken alle volgende requests de nieuwe hash, zolang de applicatie draait.

appsettings.json wordt niet aangepast (zie Beperkingen). De nieuwe hash staat in de log en kan handmatig in appsettings.json worden gezet. Lukt het vernieuwen niet, dan stopt het scrapen voor Airbnb en wordt een Error gelogd.

### Zoekparameters voor prijzen
Prijzen horen bij een zoekopdracht. De zoekparameters staan in de sectie SearchParameters en zijn gelijk voor alle platforms, zodat prijzen vergelijkbaar zijn. De aankomstdatum is de scrapedatum plus CheckInOffsetDays. Het aantal nachten is Nights, of meer als de accommodatie op de aankomstdatum een langere minimale verblijfsduur heeft, omdat het platform anders geen prijs geeft. Het aantal gasten staat in Adults, Children en Pets.

Bij elke kamer worden de gebruikte zoekparameters opgeslagen (CheckInDate, CheckOutDate, AdultCount, ChildCount en PetCount). Omdat het aantal nachten per accommodatie kan verschillen, worden prijzen vergeleken als prijs per nacht. Is een accommodatie op de aankomstdatum niet beschikbaar, dan is er die dag geen prijs.

De scraper zoekt alleen met volwassenen (zie Beperkingen). Is Children groter dan 0 in de configuratie, dan stopt de Booking-operatie met een foutmelding.

### Opslag
De listings worden per batch van 100 weggeschreven door WriteToDb, met één SaveChanges per batch. Zo krijgt niet elke listing een eigen databaseaanroep, terwijl een mislukte batch maximaal 100 listings kost. De platforms schrijven nooit tegelijk naar de database. WriteToDb gebruikt daarvoor één gedeeld slot voor alle platforms.

Een listing wordt nooit bijgewerkt. Alleen als een inhoudelijk veld gewijzigd is ten opzichte van de laatst opgeslagen versie, wordt een nieuwe rij vastgelegd. Een review krijgt alleen een nieuwe rij als het aantal reviews of de gemiddelde score gewijzigd is. De beschikbaarheid wordt bij elke scrape opgeslagen, gekoppeld aan de meest recente versie van de listing. Kamers en de voorzieningen per kamer worden ook bij elke scrape opgeslagen, omdat de prijs hoort bij de zoekparameters van die scrape. De voorzieningen van de hele accommodatie worden alleen opnieuw vastgelegd als de set gewijzigd is. Listings met hetzelfde registratienummer worden aan dezelfde vestiging (AccommodationEstablishment) gekoppeld.

Hetzelfde gegeven kan uit de zoekresultaten en uit de details komen. De waarde uit de details gaat dan voor, die uit de zoekresultaten is de terugvalwaarde.

Het datamodel gaat uit van maximaal één scrape per listing per dag, omdat ScrapingDate een datum is. Bij een tweede run op dezelfde dag worden de beschikbaarheid, de kamers en de voorzieningen van al gescrapete listings overgeslagen.

De tabel met beschikbaarheid is een TimescaleDB-hypertable op ScrapingDate. Een compressiebeleid comprimeert elke scrapedag zodra deze ouder is dan één dag. Woonplaats, gemeente en provincie worden bij het opslaan door RegionDbService bepaald uit de coördinaten, met een ruimtelijke query (PostGIS) op de referentietabel ref_locality_boundary. Bij een benaderde locatie kan de toewijzing afwijken (zie Beperkingen).

Een kamer-record in AccommodationRoom zonder SourceRoomId is een accommodatie zonder kamertypen, die als geheel wordt verhuurd. Listings zonder afzonderlijke kamers worden alsnog in AccommodationRoom gemodelleerd, zodat er geen onnodige extra kolommen in AccommodationListing nodig zijn.

Een kamer-record in AccommodationRoom die geen prijs bevat (TotalPrice null) is een kamer die op de aankomstdatum van de scrape niet beschikbaar voor boeking was.

De toeristenbelasting verschilt per gemeente. In Amsterdam is het 12,5% van de overnachtingsprijs, in veel andere gemeenten een vast bedrag per persoon per nacht (vaak tussen 2 en 3 euro). Bij Booking.com wordt alleen de regel toeristenbelasting opgeslagen als TaxAmount. Bij Airbnb komt TaxAmount uit de regel “Belastingen”, die soms meer bevat dan alleen de toeristenbelasting (zie Beperkingen).

### Foutafhandeling
Elke operatie heeft een eigen Polly-pipeline. Bij een 5xx-fout, een netwerkfout of een timeout wordt de request tot 3 keer opnieuw geprobeerd, met een oplopende wachttijd (exponential backoff). Bij een 429 (te veel requests) wordt eerst gewacht en dan één keer opnieuw geprobeerd. Zijn binnen 2 minuten alle (minimaal 3) pogingen van dezelfde operatie een 429, dan opent de circuit breaker en stopt het scrapen voor dat platform. Bij een 404 stopt het platform ook, omdat de operatie dan waarschijnlijk niet meer bestaat.

Een mislukte listing wordt overgeslagen en gelogd, de rest van de run gaat door. Een listing is mislukt wanneer minstens één van de benodigde requests, na de retries die voor dat type fout gelden, geen bruikbaar resultaat oplevert. Mislukken 3 listings achter elkaar, dan pauzeert het platform 10 minuten (cooldown) en gaat het daarna verder met de volgende listing. Na 3 cooldowns achter elkaar zonder één gelukte listing stopt het platform. De aantallen zijn instelbaar in de sectie Resilience.

Een fout in één batch slaat alleen die batch over. Bij een tijdelijke databasefout probeert EF Core het eerst 5 keer opnieuw. Blijft de database onbereikbaar, dan stopt het scrapen voor het platform. Het platform stopt ook bij een geblokkeerde request die na het vernieuwen van het aws-waf-token nog steeds een challenge-pagina geeft, en bij een persisted query hash die niet vernieuwd kan worden. Welke fout tot welke afhandeling leidt, staat op één plek in ScrapeErrorClassifier.

### Controle en rapportage
Aan het begin van elke scrape-run worden de versie van de applicatie (met de commit-hash), de request-delays en de Polly-instellingen gelogd. Zo is elke run te herleiden naar de code en de configuratie waarmee hij is uitgevoerd. Elke logregel krijgt het RunId en het platform mee, zodat de logs in Wazuh per run en per platform gefilterd kunnen worden.

Aan het einde van elk platform wordt een samenvatting gelogd met de duur, of het platform gestopt is en waarom, het aantal cooldowns, de mislukte tegels, de aantallen gevonden, unieke, buitenlandse, opgehaalde en mislukte listings, en wat er in de database is opgeslagen.

Tijdens de run wordt per listing bijgehouden of de belangrijkste velden gevuld zijn (MonitoredField). Bij lijsten, zoals de beschikbaarheid, de kamers en de voorzieningen, telt een listing als gevuld als de lijst minstens één item bevat. Is een veld na minstens 100 listings bij geen enkele listing gevuld, dan wordt er een waarschuwing gelogd. Dat wijst erop dat de responsestructuur van het platform veranderd is. Per platform staat in MonitoredFields welke velden gecontroleerd worden, omdat niet elk platform elk veld heeft. Superhost bestaat bijvoorbeeld alleen bij Airbnb.

Aan het einde van de hele run wordt de status gelogd. De status is Succeeded als geen enkele tegel mislukt is en geen enkel platform gestopt is. Cooldowns en losse mislukte listings tellen niet mee, omdat de run dan alsnog volledig doorloopt. De status is Failed als alle platforms gestopt zijn, en anders PartiallyFailed.

## Beperkingen
### Data
De gebieden zijn de top 30 gemeenten op basis van de opbrengst aan toeristenbelasting. Listings in andere gemeenten worden alleen meegenomen als ze binnen de bounding box van een van deze 30 gemeenten vallen. Analyses over gemeenten buiten de top 30 zijn daarom niet volledig.

De gebiedsindeling is zo nauwkeurig als de coördinaten van de listing. Airbnb toont bij een deel van de listings alleen een benaderde locatie, een punt binnen een straal rond de werkelijke locatie. Of de locatie exact is, wordt per listing opgeslagen in IsExactLocation.  Booking.com geeft vaker wel dan niet een exacte locatie met straatnaam en huisnummer, maar kan ook de locatie in een radius aangeven zoals Airbnb dat doet (tijdens onderzoek kwam dit overigens wel vaker voor bij Airbnb dan bij Booking.com). Bij een benaderde locatie kan een listing aan een verkeerde woonplaats worden toegewezen, en een enkele keer aan een verkeerde gemeente als de listing dicht bij een grens ligt. Een verkeerde provincie komt alleen voor als de listing vrijwel op de provinciegrens ligt. Hoe kleiner de gebiedsindeling, hoe groter de kans op een verkeerde toewijzing. Analyses per gemeente of provincie zijn daarom betrouwbaarder dan analyses per woonplaats. Bij analyses per woonplaats kan op IsExactLocation worden gefilterd. Dezelfde beperking geldt voor de controle of een Airbnb-listing in Nederland ligt. Een listing met een benaderde locatie vlak bij de landsgrens kan daardoor ten onrechte worden meegenomen of overgeslagen.

Prijzen gelden voor één zoekopdracht, met het aantal gasten, de aankomstdatum en het aantal nachten uit SearchParameters. Een prijs zegt dus wat die zoekopdracht op die scrapedag kostte, niet wat elke boeking kost.

De scraper zoekt alleen met volwassenen. Kinderen worden niet ondersteund, omdat Booking.com een leeftijd per kind vereist en Airbnb kinderen en baby’s apart telt. Die leeftijden worden niet in het datamodel opgeslagen, waardoor een prijs met kinderen niet meer te herleiden zou zijn tot de zoekopdracht. Omdat zoekparameters inclusief kinderen geen eis van de opdrachtgever zijn, is ervoor gekozen dit niet uit te werken.

De voorzieningen van de hele accommodatie zijn bij Booking.com onvolledig, omdat Booking.com de volledige lijst pas bij het scrollen laadt.

Bij Airbnb komt TaxAmount uit de regel “Belastingen”. Uit controle van de opgeslagen data bleek dat die bij een deel van de listings ook BTW bevat (bijvoorbeeld 12,5% toeristenbelasting + 21% BTW). Hierdoor kan TaxAmount bij Airbnb soms hoger zijn dan alleen de toeristenbelasting.

Per listing wordt maximaal één keer per dag gescraped. Veranderingen binnen een dag, zoals een prijswijziging in de middag, worden niet vastgelegd.

### Technisch
De parser verwacht Nederlandse teksten en m². De BaseUrl van Airbnb moet daarom https://www.airbnb.nl/ zijn. Verandert een platform de teksten in de response, dan kunnen velden leeg blijven. De controle op lege velden (zie Controle en rapportage) maakt dat zichtbaar.

Een vernieuwde persisted query hash wordt alleen in het geheugen bewaard. Na een herstart begint de scraper weer met de startwaarde uit appsettings.json en wordt de hash zo nodig opnieuw vernieuwd met Playwright.

## To do
- Analyse-component
- Presentatielaag
- Unit- en integratietests (xUnit)
