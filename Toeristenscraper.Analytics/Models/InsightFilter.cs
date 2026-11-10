namespace Toeristenscraper.Analytics.Models
{
    // record voor de filterinstellingen voor elk inzicht
    // platformId: over welk platform inzichten berekent moet worden (null = alle platforms)
    // regionLevel: welk gebiedsniveau (city, municipality, province)
    // regionName: welk specifiek gebied in het gekozen gebiedsniveau (bijvoorbeeld, wanneer regionLevel city wordt gekozen, kan regionName Heerlen gebruikt worden om alleen inzichten van Heerlen te berekenen)
    // fromDate: vanaf welke datum de inzichten berekent moeten worden
    // toDate: tot welke datum de inzichten berekent moeten worden
    // referenceDate: met welke kennis de inzichten berekent moeten worden (peildatum)
    public record InsightFilter(int? platformId, RegionLevel regionLevel, string? regionName, DateOnly fromDate, DateOnly toDate, DateOnly referenceDate);

    public enum RegionLevel {
        City = 1,
        Municipality = 2,
        Province = 3
    }
}





