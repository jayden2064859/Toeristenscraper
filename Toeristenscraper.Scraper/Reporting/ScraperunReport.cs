namespace Toeristenscraper.Scraper.Reporting
{
    public class ScraperunReport
    {
        public Guid RunId { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<PlatformRunStats> Platforms { get; set; } = new List<PlatformRunStats>();
        public List<string> StoppedPlatforms { get; set; } = new List<string>();
        public List<string> FailedTiles { get; set; } = new List<string>();
        public DateOnly ScrapingDate { get; set; }

        public string Status
        {
            get
            {
                // totalen over alle platforms
                int dutchListings = 0;
                int failedListings = 0;
                foreach (PlatformRunStats platform in Platforms)
                {
                    dutchListings += platform.Dutch;
                    failedListings += platform.FailedListings;
                }

                if (StoppedPlatforms.Count == Platforms.Count || (dutchListings > 0 && failedListings == dutchListings))
                {
                    return "Failed"; // geen enkel platform heeft de run afgemaakt, of alle listings zijn mislukt
                }
                if (StoppedPlatforms.Count > 0 || FailedTiles.Count > 0 || failedListings > 0)
                {
                    return "PartiallyFailed";
                }
                return "Succeeded";
            }
        }

        // method die aan het einde van een scraperun de statestieken van de hele run logt
        public void LogSummary(ILogger logger)
        {
            logger.LogInformation("\nScrape-run {RunId} completed: {Status} (duration {Duration})", RunId, Status, Duration);

            foreach (PlatformRunStats p in Platforms)
            {
                logger.LogInformation(
                    "\n{Platform}" +
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
                    p.PlatformName,
                    p.FoundListings, p.AreaCount, p.Duplicates, p.UniqueListings,
                    p.ForeignListings, p.Dutch,
                    p.Processed, p.FailedListings,
                    p.NewListingVersions, p.UnchangedListings, p.SkippedAlreadyScraped,
                    p.NewRooms, p.NewRoomAmenities,
                    p.NewReviews,
                    p.NewAmenitySets, p.NewAmenities,
                    p.AvailabilityRows, p.ListingsWithAvailability, ScrapingDate.AddDays(2).ToString("yyyy-MM-dd"));
            }

            string stopped = "None";
            if (StoppedPlatforms.Count > 0)
            {
                stopped = string.Join(", ", StoppedPlatforms);
            }
            string tiles = "None";
            if (FailedTiles.Count > 0)
            {
                tiles = string.Join(", ", FailedTiles);
            }
            logger.LogInformation("\nStopped platforms: {Stopped}\nFailed tiles: {FailedTiles}", stopped, tiles);
        }
    }
}

