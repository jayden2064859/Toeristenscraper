namespace Toeristenscraper.Scraper.Configuration
{
    // instellingen voor de scraper, uit de sectie "Scraper" in appsettings.json 
    public class ScraperOptions
    {
        public const string SectionName = "Scraper";

        // gebieden die elke run worden doorzocht
        public List<ScrapeArea> Areas { get; set; } = new List<ScrapeArea>();

        // instellingen per platform
        public PlatformOptions Booking { get; set; } = new PlatformOptions();
        public PlatformOptions Airbnb { get; set; } = new PlatformOptions();
        public ResilienceOptions Resilience { get; set; } = new ResilienceOptions(); 
    }

    // een gebied met de bounding box 
    public class ScrapeArea
    {
        public string Name { get; set; } = string.Empty;
        public double NeLat { get; set; }
        public double NeLng { get; set; }
        public double SwLat { get; set; }
        public double SwLng { get; set; }
    }

    // instellingen die per platform verschillen.
    public class PlatformOptions
    {
        public string BaseUrl { get; set; } = string.Empty;

        // hoe vaak een tegel maximaal mag worden opgesplitst 
        public int MaxDepth { get; set; }

        // minimale wachttijd tussen twee requests naar dit platform 
        public int RequestDelayMs { get; set; }
    }


    // model voor polly configuraties
    public class ResilienceOptions
    {
        public int MaxRetries { get; set; }
        public int RetryDelaySeconds { get; set; }
        public int RateLimitMaxRetries { get; set; }
        public int RateLimitDelaySeconds { get; set; } 
        public int CircuitMinimumThroughput { get; set; }
        public int CircuitSamplingSeconds { get; set; }
        public double CircuitFailureRatio { get; set; }
        public int TimeoutSeconds { get; set; }
    }
}