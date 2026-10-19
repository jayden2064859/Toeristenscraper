using System;
using System.Collections.Generic;
using System.Text;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.SourceAbstraction.Discovery
{
    public class Deduplicate
    {
        // methode die de originele volledige DiscoveredAccommodation lijst als argument neemt, deze lijst filtert door duplicaten te verwijderen, en de gefilterde lijst terug te geven
        // nodig omdat dezelfde accommodatie-listing meerdere keren gescraped kan worden doordat het dichtbij een grensgebied van een tegelsplitsing ligt, en daardoor in verschillende tegel-scrapes opgehaald wordt.
        public static List<DiscoveredAccommodation> DeduplicateScrapedListings(List<DiscoveredAccommodation> unfilteredList)
        {
            // lijst maken om gevonden source-ids van listings in op te slaan
            // hiermee gaat in de foreach loop gecontroleerd worden of de huidig doorlopen listing source-id al eerder voorgekomen is
            var seenSourceIds = new HashSet<string>();

            // nieuwe lijst aanmaken voor de gededupliceerde set
            var deduplicatedSet = new List<DiscoveredAccommodation>();

            foreach (var listing in unfilteredList)
            {
                string sourceId = listing.SourceId;

                if (!seenSourceIds.Contains(sourceId))
                {
                    seenSourceIds.Add(sourceId);
                    deduplicatedSet.Add(listing);
                }

            }
            return deduplicatedSet;
        }
    }
}
