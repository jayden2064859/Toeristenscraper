using System;
using System.Collections.Generic;
using System.Text;
using Toeristenscraper.Scraper.Configuration;

namespace Toeristenscraper.Scraper.Configurations
{
    public class AirbnbOptions : PlatformOptions
    {
        public string ApiKey { get; set; } = string.Empty; // elke Airbnb operatie vereist de (publieke) API-key
        public string StaysPdpSectionsHash { get; set; } = string.Empty; // persisted query hash nodig voor StaysPdpSections operatie
    }
}
