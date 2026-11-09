namespace Toeristenscraper.Scraper.Reporting
{
    // class met properties die de scrape-run stats van een individueel platform bevat
    public class PlatformRunStats
    {
        // properties die ScrapeOrchestrator vult
        public string PlatformName { get; set; } = string.Empty;
        public int AreaCount { get; set; }
        public int FoundListings { get; set; }
        public int UniqueListings { get; set; }
        public int ForeignListings { get; set; }
        public int FailedListings { get; set; }
        public int FailedTiles { get; set; }
        public bool Stopped { get; set; } = false;
        public int ProcessedListings { get; set; }

        // properties die WriteToDb vult
        public int NewListingVersions { get; set; }
        public int UnchangedListings { get; set; }
        public int SkippedAlreadyScraped { get; set; }
        public int NewReviews { get; set; }
        public int NewRooms { get; set; }
        public int NewAmenities { get; set; }
        public int NewRoomAmenities { get; set; }
        public int NewAmenitySets { get; set; }
        public int AvailabilityRows { get; set; }
        public int ListingsWithAvailability { get; set; }

        // afgeleide properties (alleen get nodig) 
        public int Duplicates => FoundListings - UniqueListings;
        public int Dutch => UniqueListings - ForeignListings;

        // platform is gefaald als het gestopt is (door Polly foutafhandeling), als er niets gevonden is door mislukte tegels, of als alle Nederlandse listings mislukt zijn
        public bool IsFailed
        {
            get
            {
                if (Stopped)
                {
                    return true;
                }
                if (FoundListings == 0 && FailedTiles > 0)
                {
                    return true;
                }
                if (Dutch > 0 && FailedListings == Dutch)
                {
                    return true;
                }
                return false;
            }
        }


    }
}
