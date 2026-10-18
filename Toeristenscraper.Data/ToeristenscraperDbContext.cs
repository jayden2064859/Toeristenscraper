using Microsoft.EntityFrameworkCore;



namespace Toeristenscraper.Data
{
    public class ToeristenscraperDbContext : DbContext
    {
        public ToeristenscraperDbContext(DbContextOptions<ToeristenscraperDbContext> options) : base(options)
        {
        }

        // initialise alle models die aan de db toegevoegd moeten worden
        //public DbSet<AccommodationListing> AccommodationListing{ get; set; }
        //public DbSet<AccommodationEstablishment> AccommodationEstablishment { get; set; }
        //public DbSet<AccommodationRoom> AccommodationRoom { get; set; }
        //public DbSet<AccommodationAmenity> AccommodationAmenity { get; set; }
        //public DbSet<AccommodationRoomAmenity> AccommodationRoomAmenity { get; set; }
        //public DbSet<Platform> Platform { get; set; }
        //public DbSet<AccommodationAvailability> AccommodationAvailability { get; set; }
        //public DbSet<AccommodationReview> AccommodationReview { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("postgis"); 
            modelBuilder.HasPostgresExtension("timescaledb"); 


        }
    }
}