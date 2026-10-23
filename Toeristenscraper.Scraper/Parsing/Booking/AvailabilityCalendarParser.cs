using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;

namespace Toeristenscraper.Scraper.Parsing.Booking
{
    public static class AvailabilityCalendarParser
    {
        public record AvailabilityCalendarPage(IReadOnlyList<Availability> Calendars);

        public static AvailabilityCalendarPage Parse(JsonDocument doc)
        {
            // eerst checken of de response de juiste data bevat (correcte typeName in de response)
            JsonElement? typeName = GetJsonElementHelper.Get(doc.RootElement, "data", "availabilityCalendar", "__typename");
            if (typeName != null && typeName.Value.GetString() == "AvailabilityCalendarQueryError")
            {
                JsonElement? message = GetJsonElementHelper.Get(doc.RootElement, "data", "availabilityCalendar", "message"); 
                string errorMessage = "unknown";
                if (message != null)
                {
                    errorMessage = message.Value.GetString() ?? "unknown";
                }
                throw new InvalidOperationException($"AvailabilityCalendar: error response from Booking.com: {errorMessage}");
            }

            JsonElement days = GetJsonElementHelper.Get(doc.RootElement, "data", "availabilityCalendar", "days")
                ?? throw new InvalidOperationException("AvailabilityCalendar: missing required fields inside JSON-response: data.availabilityCalendar.days");

            var availabilityDates = new List<Availability>();

            foreach (JsonElement day in days.EnumerateArray())
            {
                int? minLengthOfStay = null;
                JsonElement? minLengthOfStayElement = GetJsonElementHelper.Get(day, "minLengthOfStay");
                if (minLengthOfStayElement != null)
                {
                    minLengthOfStay = minLengthOfStayElement.Value.GetInt32();
                }

                DateOnly? availabilityDate = null;
                JsonElement? availabilityDateElement = GetJsonElementHelper.Get(day, "checkin");
                if (availabilityDateElement != null)
                {
                    string? dateText = availabilityDateElement.Value.GetString();
                    if (dateText != null)
                    {
                        availabilityDate = DateOnly.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                }

                bool? isAvailable = null;
                JsonElement? isAvailableElement = GetJsonElementHelper.Get(day, "available");
                if (isAvailableElement != null)
                {
                    isAvailable = isAvailableElement.Value.GetBoolean();
                }

                availabilityDates.Add(new Availability
                {
                    MinLengthOfStay = minLengthOfStay,
                    AvailabilityDate = availabilityDate,
                    IsAvailable = isAvailable

                });
            }
            return new AvailabilityCalendarPage(availabilityDates);


        }
    }
}


