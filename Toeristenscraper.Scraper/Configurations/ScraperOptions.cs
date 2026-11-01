using Toeristenscraper.Scraper.Configurations;

namespace Toeristenscraper.Scraper.Configuration
{
    // instellingen voor de scraper, uit de sectie "Scraper" in appsettings.json 
    public class ScraperOptions
    {
        public const string SectionName = "Scraper";

        // gebieden die elke run worden doorzocht
        public List<ScrapeArea> Areas { get; set; } = new List<ScrapeArea>();

        // instellingen per platform
        public PlatformOptions Booking { get; set; } = new PlatformOptions(); // booking heeft geen andere configuraties nodig dan de basis PlatformOptions 
        public AirbnbOptions Airbnb { get; set; } = new AirbnbOptions(); // airbnb heeft persisted querys en een API key nodig, dus die erft over van PlatformOptions, met twee extra properties
        public ResilienceOptions Resilience { get; set; } = new ResilienceOptions();
        public SearchParameterOptions SearchParameters { get; set; } = new SearchParameterOptions();
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
        public int CircuitBreakDurationSeconds { get; set; }
        public double CircuitFailureRatio { get; set; }
        public int TimeoutSeconds { get; set; }
    }

    // zoekparameters voor detailoperaties die prijzen teruggeven, gelijk voor alle platforms zodat prijzen vergelijkbaar zijn
    public class SearchParameterOptions
    {
        public int CheckInOffsetDays { get; set; }
        public int Nights { get; set; }
        public int Adults { get; set; }
        public int Children { get; set; }
        public int Pets { get; set; }
    }
}