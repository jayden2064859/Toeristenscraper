using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using System.Net.Http.Headers;
using Toeristenscraper.Data;
using Toeristenscraper.Scraper;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Configurations;
using Toeristenscraper.Scraper.Orchestration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Airbnb;
using Toeristenscraper.Scraper.SourceAbstraction.Airbnb.Operations;
using Toeristenscraper.Scraper.SourceAbstraction.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Booking.Operations;
using Toeristenscraper.Scraper.SourceAbstraction.Discovery;
using Toeristenscraper.Scraper.Storage;
using Toeristenscraper.Scraper.Transport;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// option classes registreren als service (waarden staan geconfigureerd in appsettings.json)
builder.Services.Configure<ScraperOptions>(
    builder.Configuration.GetSection(ScraperOptions.SectionName));


builder.Services.Configure<ScheduleOptions>(
        builder.Configuration.GetSection(ScheduleOptions.SectionName));

// DbContext registreren als service
// EnableRetryOnFailure: bij een tijdelijke databasefout probeert EF de operatie eerst 5 keer opnieuw, daarna wordt de fout doorgegeven aan de orchestrator (platform run wordt gestopt)
builder.Services.AddDbContext<ToeristenscraperDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("ToeristenDb"), x =>
    {
        x.UseNetTopologySuite();
        x.EnableRetryOnFailure(5);
    }));
var bookingBase = builder.Configuration["Scraper:Booking:BaseUrl"];
var airbnbBase = builder.Configuration["Scraper:Airbnb:BaseUrl"];

// http-clients registreren
IHttpClientBuilder staysSearchClient = builder.Services.AddHttpClient<StaysSearch>(client =>
{
    if (!string.IsNullOrWhiteSpace(airbnbBase))
    {
        client.BaseAddress = new Uri(airbnbBase);
    }
    client.DefaultRequestHeaders.Add("X-Airbnb-API-Key", builder.Configuration["Scraper:Airbnb:ApiKey"]);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromMinutes(3);
});

IHttpClientBuilder pdpAvailabilityCalendarClient = builder.Services.AddHttpClient<PdpAvailabilityCalendar>(client =>
{
    if (!string.IsNullOrWhiteSpace(airbnbBase))
    {
        client.BaseAddress = new Uri(airbnbBase);
    }
    client.DefaultRequestHeaders.Add("X-Airbnb-API-Key", builder.Configuration["Scraper:Airbnb:ApiKey"]);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromMinutes(3);
});

IHttpClientBuilder fullSearchClient = builder.Services.AddHttpClient<FullSearch>(client =>
{
    if (!string.IsNullOrWhiteSpace(bookingBase))
    {
        client.BaseAddress = new Uri(bookingBase);
    }
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromMinutes(3); 
});

IHttpClientBuilder availabilityCalendarClient = builder.Services.AddHttpClient<AvailabilityCalendar>(client =>
{
    if (!string.IsNullOrWhiteSpace(bookingBase))
    {
        client.BaseAddress = new Uri(bookingBase);
    }
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromMinutes(3);
});

// per http-client de request delay + resilience strategy handlers toevoegen
staysSearchClient.AddHttpMessageHandler<AirbnbRequestDelayHandler>();
staysSearchClient.AddResilienceHandler("airbnb-staysearch", ResiliencePipelineSetup.Configure);

pdpAvailabilityCalendarClient.AddHttpMessageHandler<AirbnbRequestDelayHandler>();
pdpAvailabilityCalendarClient.AddResilienceHandler("airbnb-availability", ResiliencePipelineSetup.Configure);

fullSearchClient.AddHttpMessageHandler<BookingRequestDelayHandler>();
fullSearchClient.AddResilienceHandler("booking-fullsearch", ResiliencePipelineSetup.Configure);

availabilityCalendarClient.AddHttpMessageHandler<BookingRequestDelayHandler>();
availabilityCalendarClient.AddResilienceHandler("booking-availability", ResiliencePipelineSetup.Configure);

//  transient services registreren (nieuwe instantie per request) 
builder.Services.AddTransient<TileAlgorithm>();
builder.Services.AddTransient<ScrapeOrchestrator>();
builder.Services.AddTransient<BookingRequestDelayHandler>();
builder.Services.AddTransient<AirbnbRequestDelayHandler>();
//builder.Services.AddTransient<IAccommodationSource, BookingSource>();
builder.Services.AddTransient<IAccommodationSource, AirbnbSource>();
builder.Services.AddScoped<WriteToDb>();

builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");

var host = builder.Build();
host.Run();
