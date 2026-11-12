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
                    PlatformName, PlatformRunDuration,
                    stopped, failedTiles,
                    FoundListingsCount, AreaCount, DuplicatesCount, UniqueListingsCount,
                    ForeignListingsCount, ValidListingsCount,
                    ProcessedListingsCount, FailedListingsCount,
                    NewListingVersionsCount, UnchangedListingsCount, SkippedAlreadyScrapedCount,
                    NewRoomsCount, NewRoomAmenitiesCount,
                    NewReviewsCount,
                    NewAmenitySetsCount, NewAmenitiesCount,
                    AvailabilityRowsCount, ListingsWithAvailabilityCount, scrapingDate.AddDays(2).ToString("yyyy-MM-dd"));
        }
    }
}
