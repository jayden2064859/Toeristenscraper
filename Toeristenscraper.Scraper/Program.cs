using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net.Http.Headers;
using Toeristenscraper.Data;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.Discovery;
using Toeristenscraper.Scraper.Hosting;
using Toeristenscraper.Scraper.Orchestration;
using Toeristenscraper.Scraper.SourceAbstraction;
using Toeristenscraper.Scraper.SourceAbstraction.Airbnb;
using Toeristenscraper.Scraper.SourceAbstraction.Airbnb.HttpServices;
using Toeristenscraper.Scraper.SourceAbstraction.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Booking.HttpServices;
using Toeristenscraper.Scraper.Storage;
using Toeristenscraper.Scraper.Transport;

// simpele console logger waar opstartfouten in komen te staan, nog voordat serilog wordt geladen.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSystemd();

    // connection naar Azure Key Vault (voor productie)
    if (builder.Environment.IsProduction())
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(builder.Configuration["KeyVaultUri"]!),
            new DefaultAzureCredential()); // managed identity, zodat secrets niet gehardcode hoeven te worden
    }

    // DbContext registreren als service
    builder.Services.AddDbContext<ToeristenscraperDbContext>(o =>
        o.UseNpgsql(builder.Configuration.GetConnectionString("ToeristenDb"), x =>
        {
            x.UseNetTopologySuite();
            x.EnableRetryOnFailure(5); //bij een tijdelijke databasefout probeert EF de operatie eerst 5 keer opnieuw, daarna wordt de fout doorgegeven aan de orchestrator(platform run wordt gestopt)
        }));
    var bookingBase = builder.Configuration["Scraper:Booking:BaseUrl"];
    var airbnbBase = builder.Configuration["Scraper:Airbnb:BaseUrl"];


    builder.Services.AddHostedService<Worker>();

    // option classes registreren als service (waarden staan geconfigureerd in appsettings.json)
    builder.Services.Configure<ScraperOptions>(
        builder.Configuration.GetSection(ScraperOptions.SectionName));

    builder.Services.Configure<ScheduleOptions>(
            builder.Configuration.GetSection(ScheduleOptions.SectionName));


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

    IHttpClientBuilder staysPdpSectionsClient = builder.Services.AddHttpClient<StaysPdpSections>(client =>
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

    IHttpClientBuilder htmlClient = builder.Services.AddHttpClient<HTML>(client =>
    {
        if (!string.IsNullOrWhiteSpace(bookingBase))
        {
            client.BaseAddress = new Uri(bookingBase);
        }
        // user agent header is nodig, anders ziet Booking.com de HTML requests als bot requests, en worden de zoekparameters volledig genegeerd, waardoor kamerprijzen nooit teruggegeven worden
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("nl-NL,nl;q=0.9");
        client.Timeout = TimeSpan.FromMinutes(3);
    }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false });


    // per http-client de request delay + resilience strategy handlers toevoegen
    // voor de request delay van elke httpClient wordt lambda gebruikt om een nieuwe handler te maken. Elke handler moet een nieuwe instantie zijn omdat DelegatingHandler (klasse waar RequestDelayHandler.cs van erft) dat vereist
    
    // Airbnb
    staysSearchClient.AddResilienceHandler("airbnb-staysearch", ResiliencePipelineSetup.Configure);
    staysSearchClient.AddHttpMessageHandler(sp => new RequestDelayHandler(sp.GetRequiredService<IOptions<ScraperOptions>>().Value.Airbnb.RequestDelayMs));

    pdpAvailabilityCalendarClient.AddResilienceHandler("airbnb-availability", ResiliencePipelineSetup.Configure);
    pdpAvailabilityCalendarClient.AddHttpMessageHandler(sp => new RequestDelayHandler(sp.GetRequiredService<IOptions<ScraperOptions>>().Value.Airbnb.RequestDelayMs));

    staysPdpSectionsClient.AddResilienceHandler("airbnb-stayspdpsections", ResiliencePipelineSetup.Configure);
    staysPdpSectionsClient.AddHttpMessageHandler(sp => new RequestDelayHandler(sp.GetRequiredService<IOptions<ScraperOptions>>().Value.Airbnb.RequestDelayMs));

    // Booking.com
    fullSearchClient.AddResilienceHandler("booking-fullsearch", ResiliencePipelineSetup.Configure);
    fullSearchClient.AddHttpMessageHandler(sp => new RequestDelayHandler(sp.GetRequiredService<IOptions<ScraperOptions>>().Value.Booking.RequestDelayMs));

    availabilityCalendarClient.AddResilienceHandler("booking-availability", ResiliencePipelineSetup.Configure);
    availabilityCalendarClient.AddHttpMessageHandler(sp => new RequestDelayHandler(sp.GetRequiredService<IOptions<ScraperOptions>>().Value.Booking.RequestDelayMs));

    htmlClient.AddResilienceHandler("booking-html", ResiliencePipelineSetup.Configure);
    htmlClient.AddHttpMessageHandler(sp => new RequestDelayHandler(sp.GetRequiredService<IOptions<ScraperOptions>>().Value.Booking.RequestDelayMs));


    // transient services registreren (nieuwe instantie per request) 
    builder.Services.AddTransient<TileAlgorithm>();
    builder.Services.AddTransient<ScrapeOrchestrator>();
    builder.Services.AddTransient<IAccommodationSource, BookingSource>();
    builder.Services.AddTransient<IAccommodationSource, AirbnbSource>();

    builder.Services.AddScoped<WriteToDb>();

    builder.Services.AddSingleton<AirbnbHashProvider>();
    builder.Services.AddSingleton<BookingCookieProvider>();

    builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(builder.Configuration));

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Toeristenscraper stopped unexpectedly");
    Environment.ExitCode = 1; // zodat de VM kan zien dat de applicatie gecrashed is
}
finally
{
    Log.CloseAndFlush();
}
