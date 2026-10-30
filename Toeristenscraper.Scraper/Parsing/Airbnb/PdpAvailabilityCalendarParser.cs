using System.Globalization;
using System.Text.Json;
using Toeristenscraper.Scraper.Parsing.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Parsing.Airbnb
{
    public record PdpAvailabilityCalendarPage(IReadOnlyList<Availability> Calendars);
    public static class PdpAvailabilityCalendarParser
    {
        public static PdpAvailabilityCalendarPage Parse(JsonDocument doc, DateOnly scrapingDate)
        {
            // JSON element waar de daadwerkelijke beschikbaarheidsdagen in zitten, gesorteerd in aparte objecten voor elke kalendermaand
            JsonElement calendarMonths = GetJsonElementHelper.Get(doc.RootElement, "data", "merlin", "pdpAvailabilityCalendar", "calendarMonths")
                ?? throw new InvalidOperationException("PdpAvailabilityCalendar: missing required fields inside JSON-response: data.merlin.pdpAvailabilityCalendar.calendarMonths");

            List<Availability> availabilityDates = new List<Availability>();

            // elk object voor elke maand doorlopen om de dagen eruit te halen
            foreach (JsonElement calendarMonth in calendarMonths.EnumerateArray())
            {
                JsonElement? calendarDays = GetJsonElementHelper.Get(calendarMonth, "days");
                if (calendarDays == null)
                {
                    continue; // maanden die geen data bevatten worden overgeslagen
                }

                // elke dag binnen elke maand ophalen
                foreach (JsonElement day in calendarDays.Value.EnumerateArray())
                {
                    // beschikbaarheidsdatum
                    DateOnly? availabilityDate = null;
                    string? calendarDate = GetJsonElementHelper.Get(day, "calendarDate")?.GetString();
                    if (calendarDate != null)
                    {
                        availabilityDate = DateOnly.ParseExact(calendarDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }

                    // verlopen dagen mogen niet opgeslagen worden. Airbnb geeft verlopen dagen altijd als niet beschikbaar aan, wat geen daadwerkelijke bezettingsdata is
                    if (availabilityDate != null && availabilityDate.Value < scrapingDate)
                    {
                        continue; // als de beschikbaarheidsdatum eerder dan de scrapedatum is, wordt die beschikbaarheidsdatum overgeslagen 
                    }

                    // beschikbaarheid (true/false)
                    bool? isAvailable = null;
                    JsonElement? isAvailableElement = GetJsonElementHelper.Get(day, "available");
                    if (isAvailableElement != null)
                    {
                        isAvailable = isAvailableElement.Value.GetBoolean();
                    }

                    // minimale verblijfsduur
                    int? minLengthOfStay = null;
                    JsonElement? minLengthOfStayElement = GetJsonElementHelper.Get(day, "minNights");
                    if (minLengthOfStayElement != null)
                    {
                        minLengthOfStay = minLengthOfStayElement.Value.GetInt32();
                    }

                    // maximale verblijfsduur
                    int? maxLengthOfStay = null;
                    JsonElement? maxLengthOfStayElement = GetJsonElementHelper.Get(day, "maxNights");
                    if (maxLengthOfStayElement != null)
                    {
                        maxLengthOfStay = maxLengthOfStayElement.Value.GetInt32();
                    }

                    // nieuw availability object aanmaken en toevoegen aan de lijst
                    availabilityDates.Add(new Availability
                    {
                        AvailabilityDate = availabilityDate,
                        IsAvailable = isAvailable,
                        MinLengthOfStay = minLengthOfStay,
                        MaxLengthOfStay = maxLengthOfStay
                    });
                }

            }
            return new PdpAvailabilityCalendarPage(availabilityDates);
        }
         
    }
}

