using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http.Headers;
using Toeristenscraper.Data;
using Toeristenscraper.Scraper;
using Toeristenscraper.Scraper.Configuration;
using Toeristenscraper.Scraper.SourceAbstraction.Abstractions;
using Toeristenscraper.Scraper.SourceAbstraction.Booking;
using Toeristenscraper.Scraper.SourceAbstraction.Discovery;


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

// ScraperOptions registreren als service (de 'Scraper' sectie uit appsetting.json wordt gekoppeld aan de modellen in ScraperOptions)
builder.Services.Configure<ScraperOptions>(
    builder.Configuration.GetSection(ScraperOptions.SectionName));

// DbContext registreren als service
builder.Services.AddDbContext<ToeristenscraperDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("ToeristenDb"),
                x => x.UseNetTopologySuite()));

// TileAlgorithm registreren als service
builder.Services.AddTransient<TileAlgorithm>();

// FullSearch registreren als http-client zodat het kan worden geïnjecteerd in de worker
var bookingBase = builder.Configuration["Scraper:Booking:BaseUrl"];
builder.Services.AddHttpClient<FullSearch>(client =>
{
    if (!string.IsNullOrWhiteSpace(bookingBase))
    {
        client.BaseAddress = new Uri(bookingBase);
    }
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddTransient<IAccommodationSource, BookingSource>();

var host = builder.Build();
host.Run();
