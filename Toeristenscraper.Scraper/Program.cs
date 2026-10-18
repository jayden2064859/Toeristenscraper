using Microsoft.EntityFrameworkCore;
using Toeristenscraper.Data;
using Toeristenscraper.Scraper;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddDbContext<ToeristenscraperDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("ToeristenDb"),
                x => x.UseNetTopologySuite()));

var host = builder.Build();
host.Run();
