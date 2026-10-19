using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Toeristenscraper.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvailabilityHypertable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // hypertable op ScrapingDate, één chunk per scrapedag
            migrationBuilder.Sql(@"
        SELECT create_hypertable('""AccommodationAvailability""', 'ScrapingDate',
                                 chunk_time_interval => INTERVAL '1 day');");

            // compressie: per listing gegroepeerd, gesorteerd op beschikbaarheidsdatum
            migrationBuilder.Sql(@"
        ALTER TABLE ""AccommodationAvailability"" SET (
            timescaledb.compress,
            timescaledb.compress_segmentby = '""AccommodationListingId""',
            timescaledb.compress_orderby  = '""AvailabilityDate""');");

            // chunks ouder dan 1 dag worden automatisch gecomprimeerd
            migrationBuilder.Sql(@"
        SELECT add_compression_policy('""AccommodationAvailability""', INTERVAL '1 day');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"SELECT remove_compression_policy('""AccommodationAvailability""', if_exists => true);");
        }
    }
}
