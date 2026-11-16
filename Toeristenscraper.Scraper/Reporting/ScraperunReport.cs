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
                    if (platform.FailedTilesCount > 0)
                    {
                        errorOccurred = true;
                    }
                }

                if (failedPlatforms == Platforms.Count)
                {
                    // een scrape-run wordt alleen als 'Failed' gezien als beide platforms gestopt zijn tijdens de run
                    return "Failed";
                }
                if (failedPlatforms > 0 || errorOccurred)
                {
                    // een scrape-run wordt als PartiallyFailed gezien als er minstens 1 platform gestopt is, of wanneer er minstens 1 gehele tegel tijdens discovery mislukt is
                    return "PartiallyFailed";
                }
                // een scrape-run is succeeded wanneer er geen platforms gestopt zijn en geen individuele tegels mislukt zijn tijdens discovery. Losse listings mogen falen, die worden individueel overgeslagen en wordt afgehandeld met Polly.
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

