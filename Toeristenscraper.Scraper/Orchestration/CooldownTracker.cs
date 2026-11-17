using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Exceptions;
using Toeristenscraper.Scraper.Reporting;

namespace Toeristenscraper.Scraper.Orchestration
{
    // aparte class om bij te houden hoeveel gefaalde achtereenvolgende requests uitgevoerd zijn. bij het max ingestelde aantal volgt een cooldown van een ingestelde tijd. Een cooldown mag een ingesteld maximaal aantal keer uitgevoerd worden voordat de platform-run stopt
    // alle configuraties staan in appsettings.json
    public class CooldownTracker(ResilienceOptions options, PlatformRunStats platformRunStats, ILogger logger)
    {
        // tellers initialiseren
        int consecutiveFailures = 0; // aantal listings dat achter elkaar mislukt is. bij MaxConsecutiveFailures volgt een cooldown
        
        int cooldownsWithoutSuccess = 0; // aantal cooldowns achter elkaar zonder dat er tussendoor een listing gelukt is. bij MaxCooldowns stopt het platform

        public void ResetCounters() // tellers worden gereset na een succesvolle request
        {
            consecutiveFailures = 0;
            cooldownsWithoutSuccess = 0;
        }

        // methode om een gefaalde request toe te voegen aan de consecutiveFailures teller, waarna gecontroleerd wordt of de ingestelde limiet bereikt is (zo ja, dan start een cooldown) 
        public async Task RegisterFailureAsync(CancellationToken ct)
        {
            consecutiveFailures += 1;

            // wordt uitgevoerd wanneer MaxConsecutiveFailures listings achter elkaar mislukt zijn (initial request + 3 retries): cooldown, of stoppen als er al MaxCooldowns cooldowns zonder succes zijn geweest
            if (consecutiveFailures >= options.MaxConsecutiveFailures)
            {
                // nog niet alle cooldowns achter elkaar gebruikt: platform pauzeert een ingesteld aantal minuten en gaat daarna verder met de volgende listing
                if (cooldownsWithoutSuccess < options.MaxCooldowns)
                {
                    cooldownsWithoutSuccess += 1;
                    platformRunStats.CooldownCount += 1;

                    logger.LogWarning("{Platform}: {Failures} consecutive failed listings, pausing {Minutes} minutes (cooldown {Count}/{Max} without success)",
                        platformRunStats.PlatformName, consecutiveFailures, options.CooldownMinutes, cooldownsWithoutSuccess, options.MaxCooldowns);

                    await Task.Delay(TimeSpan.FromMinutes(options.CooldownMinutes), ct);
                    consecutiveFailures = 0;
                }
                // alle cooldowns achter elkaar gebruikt zonder één gelukte listing: platform stopt
                else
                {
                    throw new MaxConsecutiveFailuresException("No successful listing after " + options.MaxCooldowns + " consecutive cooldowns");
                }
            }
        }
    }
}




