namespace Toeristenscraper.Scraper.Reporting
{
    public class ScraperunReport
    {
        public Guid RunId { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<PlatformRunStats> Platforms { get; set; } = new List<PlatformRunStats>();
        public string Status
        {
            get
            {
                int failedPlatforms = 0;
                bool errorOccurred = false;
                foreach (PlatformRunStats platform in Platforms)
                {
                    if (platform.IsFailed)
                    {
                        failedPlatforms += 1;
                    }
                    if (platform.FailedTilesCount > 0 || platform.FailedListingsCount > 0)
                    {
                        errorOccurred = true;
                    }
                }

                if (failedPlatforms == Platforms.Count)
                {
                    return "Failed";
                }
                if (failedPlatforms > 0 || errorOccurred)
                {
                    return "PartiallyFailed";
                }
                return "Succeeded";
            }
        }

        public void LogSummary(ILogger logger)
        {
            if (Status == "Failed")
            {
                logger.LogError("Scrape-run {RunId} completed: {Status} (duration {Duration:hh\\:mm\\:ss})", RunId, Status, Duration);
            }
            else if (Status == "PartiallyFailed")
            {
                logger.LogWarning("Scrape-run {RunId} completed: {Status} (duration {Duration:hh\\:mm\\:ss})", RunId, Status, Duration);
            }
            else
            {
                logger.LogInformation("Scrape-run {RunId} completed: {Status} (duration {Duration:hh\\:mm\\:ss})", RunId, Status, Duration);
            }

            // gestopte platforms en mislukte tegels worden afgeleid uit de stats per platform
            List<string> stoppedPlatforms = new List<string>();
            List<string> failedTiles = new List<string>();
            foreach (PlatformRunStats platform in Platforms)
            {
                if (platform.Stopped)
                {
                    stoppedPlatforms.Add(platform.PlatformName + " (" + platform.StopReason + ")");
                }
                foreach (string tileName in platform.FailedTileNames)
                {
                    failedTiles.Add(platform.PlatformName + "/" + tileName);
                }
            }

            string stopped = "None";
            if (stoppedPlatforms.Count > 0)
            {
                stopped = string.Join(", ", stoppedPlatforms);
            }
            string tiles = "None";
            if (failedTiles.Count > 0)
            {
                tiles = string.Join(", ", failedTiles);
            }
            logger.LogInformation("Stopped platforms: {Stopped}, Failed tiles: {FailedTiles}", stopped, tiles);
        }
    }
}

