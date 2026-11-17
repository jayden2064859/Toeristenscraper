using Toeristenscraper.Scraper.Models;

namespace Toeristenscraper.Scraper.Reporting
{
    // class met properties die de scrape-run stats van een individueel platform bevat
    public class PlatformRunStats
    {
        // properties die ScrapeOrchestrator vult
        public string PlatformName { get; set; } = string.Empty;
        public int AreaCount { get; set; }
        public int FoundListingsCount { get; set; }
        public int UniqueListingsCount { get; set; }
        public int ForeignListingsCount { get; set; }
        public int FailedListingsCount { get; set; }
        public int CooldownCount { get; set; }
        public List<string> FailedTileNames { get; set; } = new List<string>();
        public List<string> ListingsPerArea { get; set; } = new List<string>();
        public int FailedTilesCount
        {
            get
            {
                int amount = FailedTileNames.Count;
                return amount;
            }
        }
        public string? StopReason { get; set; } = null;
        public bool Stopped
        {
            get
            {
                if (StopReason != null)
                {
                    return true;
                }
                return false;
            }
        }
        public int ProcessedListingsCount { get; set; }
        public TimeSpan PlatformRunDuration { get; set; }

        // properties die WriteToDb vult
        public int NewListingVersionsCount { get; set; }
        public int UnchangedListingsCount { get; set; }
        public int SkippedAlreadyScrapedCount { get; set; }
        public int NewReviewsCount { get; set; }
        public int NewRoomsCount { get; set; }
        public int NewAmenitiesCount { get; set; }
        public int NewRoomAmenitiesCount { get; set; }
        public int NewAmenitySetsCount { get; set; }
        public int AvailabilityRowsCount { get; set; }
        public int ListingsWithAvailabilityCount { get; set; }

        // afgeleide properties (alleen get nodig) 
        public int DuplicatesCount => FoundListingsCount - UniqueListingsCount;
        // alleen nederlandse listings zijn geldige listings voor de scope van dit project 
        public int ValidListingsCount => UniqueListingsCount - ForeignListingsCount;

        // platform is gefaald als het gestopt is (door Polly foutafhandeling), als er niets gevonden is door mislukte tegels, of als alle Nederlandse listings mislukt zijn
        public bool IsFailed
        {
            get
            {
                if (Stopped)
                {
                    return true;
                }
                if (FoundListingsCount == 0 && FailedTilesCount > 0)
                {
                    return true;
                }
                if (ValidListingsCount > 0 && FailedListingsCount == ValidListingsCount)
                {
                    return true;
                }
                return false;
            }
        }

        public void LogSummary(ILogger logger, DateOnly scrapingDate)
        {
            string stopped = "No";
            if (StopReason != null)
            {
                stopped = "Yes (" + StopReason + ")";
            }

            // standaard 'None' in de logging wanneer geen tegels mislukt zijn. zo wel, dan wordt elke tegelnaam gelogd
            string failedTiles = "None";
            if (FailedTileNames.Count > 0)
            {
                failedTiles = string.Join(", ", FailedTileNames);
            }
            logger.LogInformation(
                    "{Platform} (duration {PlatformRunDuration:hh\\:mm\\:ss})" +
                    "\n  Stopped         {Stopped}" +
                    "\n  Cooldowns       {CooldownCount}" +
                    "\n  Failed tiles    {FailedTileNames}" +
                    "\n  Scraping" +
                    "\n    Discovery     {Found} found in {Areas} area(s), {Duplicates} duplicates -> {Unique} unique" +
                    "\n    Filtering     {Foreign} foreign -> {Dutch} Dutch" +
                    "\n    Retrieved     {Processed} listings with availability, rooms and prices ({Failed} failed)" +
                    "\n  Saved to database" +
                    "\n    Listings      {NewVersions} new or changed, {Unchanged} unchanged, {SkippedToday} already scraped today" +
                    "\n    Rooms         {Rooms} rooms ({RoomAmenities} room amenities)" +
                    "\n    Reviews       {Reviews} new or changed" +
                    "\n    Amenities     {AmenitySets} listings with new or changed amenities ({Amenities} amenities)" +
                    "\n    Availability  {Rows} days for {AvailabilityListings} listings, compressed from {CompressionDate}",
                    this.PlatformName, this.PlatformRunDuration,
                    stopped, this.CooldownCount, failedTiles,
                    this.FoundListingsCount, this.AreaCount, this.DuplicatesCount, this.UniqueListingsCount,
                    this.ForeignListingsCount, this.ValidListingsCount,
                    this.ProcessedListingsCount, this.FailedListingsCount,
                    this.NewListingVersionsCount, this.UnchangedListingsCount, this.SkippedAlreadyScrapedCount,
                    this.NewRoomsCount, this.NewRoomAmenitiesCount,
                    this.NewReviewsCount,
                    this.NewAmenitySetsCount, this.NewAmenitiesCount,
                    this.AvailabilityRowsCount, this.ListingsWithAvailabilityCount, scrapingDate.AddDays(2).ToString("yyyy-MM-dd"));
        }


        // onderstaande code is om voor elke sxrape-run te controleren of de huidige fields die in de parsers worden gebruikt om data mee op te halen, nog actueel zijn en nog werken.
        // er wordt bijgehouden hoeveel fields er in totaal gecontroleerd zijn, en hoe vaak er uit deze field een daadwerkelijke waarde is gehaald.
        // als alle listings waarde null hebben, is het een sterke indicatie dat de field niet meer klopt. In dit geval wordt er een Warning gelogged. 
        public int CheckedListingsCount { get; set; }
        public Dictionary<MonitoredField, int> FilledFieldCounts { get; set; } = new Dictionary<MonitoredField, int>();

        public void CountField(MonitoredField field, bool hasValue)
        {
            if (!FilledFieldCounts.ContainsKey(field))
            {
                FilledFieldCounts[field] = 0; // 0 toevoegen aan de teller als er geen waarde is gevonden 
            }
            if (hasValue)
            {
                FilledFieldCounts[field] += 1; // 1 toevoegen aan de teller als er wel een waarde is gevonden
            }
        }

        public void CheckFieldFillRates(ILogger logger, IReadOnlyList<MonitoredField> monitoredFields)
        {
            if (CheckedListingsCount < 100) // zodra 100 listings zijn gecontroleerd, wordt voor elk doorlopen field gekeken hoe hoog de teller is. Als voor 100 listings de teller nog steeds op 0 is, wordt een warning gelogged dat de field mogelijk veranderd is
            {
                return; // te weinig listings voor een betrouwbare controle
            }
            foreach (MonitoredField field in monitoredFields)
            {
                int filled = FilledFieldCounts.GetValueOrDefault(field);
                if (filled == 0)
                {
                    logger.LogWarning("{Platform}: field {Field} is empty for all {Count} listings, response structure may have changed",
                        PlatformName, field, CheckedListingsCount);
                }
            }
        }

        // methode om te controleren welke waarde voor elke field is opgehaald.
        // als alle listings waarde null hebben teruggekregen voor de specifieke field, wordt er een warning gelogged
        public void CountRetrievedFields(DiscoveredAccommodation listing, AccommodationDetails details, IReadOnlyList<Availability> availabilityDays)
        {
            CheckedListingsCount += 1;
            CountField(MonitoredField.Title, listing.Title != null);
            CountField(MonitoredField.Coordinates, listing.Latitude != null);
            CountField(MonitoredField.AccommodationType, (details.AccommodationType ?? listing.AccommodationType) != null);
            CountField(MonitoredField.Availability, availabilityDays.Count > 0);
            CountField(MonitoredField.Rooms, details.Rooms.Count > 0);
            CountField(MonitoredField.TotalPrice, details.Rooms.Any(r => r.TotalPrice != null));
            CountField(MonitoredField.Amenities, details.Amenities.Count > 0);
            CountField(MonitoredField.ReviewCount, (details.ReviewCount ?? listing.ReviewCount) != null);
            CountField(MonitoredField.KvkNumber, details.KvkNumber != null);
            CountField(MonitoredField.RegistrationNumber, details.RegistrationNumber != null);
            CountField(MonitoredField.StarClassification, listing.StarClassification != null);
            CountField(MonitoredField.Superhost, (details.Superhost ?? listing.Superhost) != null);
        }
    }
}
