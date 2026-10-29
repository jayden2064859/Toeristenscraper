using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Scraper.Configurations
{
    public class ScheduleOptions
    {
        public const string SectionName = "Schedule";
        public TimeSpan RunTime { get; set; }
        public bool RunOnStartup { get; set; } // voor tijdens development
        public string TimeZone { get; set; } = "Europe/Amsterdam";
    }
}
