using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace Toeristenscraper.Data.Entities
{
    public class AccommodationEstablishment
    {
        public int AccommodationEstablishmentId { get; set; }
        public required string RegistrationNumber { get; set; }
    }
}
