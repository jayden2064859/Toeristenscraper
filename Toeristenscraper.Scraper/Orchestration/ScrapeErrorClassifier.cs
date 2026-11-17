using Npgsql;
using Polly.CircuitBreaker;
using System.Net;
using Toeristenscraper.Scraper.Exceptions;

namespace Toeristenscraper.Scraper.Orchestration
{
    // bepaalt per soort fout hoe de orchestrator ermee omgaat (listing overslaan of platform stoppen, en wel of geen stacktrace loggen) 
    public static class ScrapeErrorClassifier
    {
        // fouten waarbij doorgaan met het platform geen zin heeft wordt het platform gestopt voor de gehele run
        public static bool StopsPlatform(Exception ex)
        {
            if (ex is BrokenCircuitException) // circuit is open (gebeurd alleen bij minimaal drie 429 responses binnen 2 minuten uit dezelfde httpClient, Polly resilience)
            {
                return true;
            }
            if (ex is HttpRequestException httpException && httpException.StatusCode == HttpStatusCode.NotFound) // 404 (not found)
            {
                return true;
            }
            if (ex is OutdatedHashException || ex is BlockedException)
            {
                return true;
            }
            if (IsDatabaseUnavailable(ex)) // database wordt 5x opnieuw geprobeerd (config in program.cs) daarna stopt het platform met scrapen
            {
                return true;
            }
            return false;
        }

        // verwachte fouten bij één listing (listing wordt overgeslagen, stacktrace wordt niet gelogged in de orchestrator)
        public static bool IsExpectedListingError(Exception ex)
        {
            if (ex is HttpRequestException || ex is GraphQLErrorException)
            {
                return true;
            }
            return false;
        }

        // verwachte fouten waardoor een platform gestopt is  (platform wordt gestopt, stacktrace wordt niet gelogged in de orchestrator)
        public static bool IsExpectedPlatformError(Exception ex)
        {
            if (IsDatabaseUnavailable(ex) || ex is BlockedException || ex is OutdatedHashException || ex is HttpRequestException || ex is MaxConsecutiveFailuresException)
            {
                return true;
            }
            return false;
        }

        // controleert of een fout (of een inner exception) wordt veroorzaakt door een onbereikbare database
        public static bool IsDatabaseUnavailable(Exception ex)
        {
            Exception? current = ex;
            while (current != null)
            {
                // transiente (tijdelijke) fouten
                if (current is NpgsqlException npgsqlException && npgsqlException.IsTransient)
                {
                    return true;
                }
                // niet-transiente fouten (bijv verkeerd wachtwoord, 28P01) wordt ook opgevangen
                if (current is PostgresException postgresException && postgresException.SqlState.StartsWith("28"))
                {
                    return true;
                }
                current = current.InnerException;
            }
            return false;
        }
    }
}