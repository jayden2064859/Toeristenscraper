using System;
using System.Collections.Generic;
using System.Text;

namespace Toeristenscraper.Data.Entities
{
    public class Platform
    {
        public int PlatformId { get; set; }
        public required string PlatformName { get; set; }
    }
}
