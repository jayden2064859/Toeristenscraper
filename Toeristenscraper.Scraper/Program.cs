using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Toeristenscraper.Data;
using Toeristenscraper.Scraper;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Orchestration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Booking.Operations;
using Toeristenscraper.Scraper.SourceAbstraction.Discovery;
using Toeristenscraper.Scraper.Storage;
using Toeristenscraper.Scraper.Transport;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// ScraperOptions registreren als service (de 'Scraper' sectie uit appsetting.json wordt gekoppeld aan de modellen in ScraperOptions)
builder.Services.Configure<ScraperOptions>(
    builder.Configuration.GetSection(ScraperOptions.SectionName));

// DbContext registreren als service
// EnableRetryOnFailure: bij een tijdelijke databasefout probeert EF de operatie eerst 5 keer opnieuw, daarna wordt de fout doorgegeven aan de orchestrator (run wordt gestopt)
builder.Services.AddDbContext<ToeristenscraperDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("ToeristenDb"), x =>
    {
        x.UseNetTopologySuite();
        x.EnableRetryOnFailure(5);
    }));
var bookingBase = builder.Configuration["Scraper:Booking:BaseUrl"];

// FullSearch registreren als http-client
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

// AvailabilityCalendar registreren als http-client
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

// eerst de wachttijd tussen requests, daarna worden de polly-strategies uitgevoerd
fullSearchClient.AddHttpMessageHandler<BookingRequestDelayHandler>();
fullSearchClient.AddResilienceHandler("booking-fullsearch", ResiliencePipelineSetup.Configure);
availabilityCalendarClient.AddHttpMessageHandler<BookingRequestDelayHandler>();
availabilityCalendarClient.AddResilienceHandler("booking-availability", ResiliencePipelineSetup.Configure);

//  transient services registreren (nieuwe instantie per request) 
builder.Services.AddTransient<TileAlgorithm>();
builder.Services.AddTransient<ScrapeOrchestrator>();
builder.Services.AddTransient<BookingRequestDelayHandler>();
builder.Services.AddTransient<IAccommodationSource, BookingSource>();
builder.Services.AddScoped<WriteToDb>();

builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");

var host = builder.Build();
host.Run();
