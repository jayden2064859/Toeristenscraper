using Microsoft.EntityFrameworkCore;
using Toeristenscraper.Data.Entities;



namespace Toeristenscraper.Data
{
    public class ToeristenscraperDbContext : DbContext
    {
        public ToeristenscraperDbContext(DbContextOptions<ToeristenscraperDbContext> options) : base(options)
        {
        }

        // initialise alle entities die aan de db toegevoegd moeten worden
        public DbSet<AccommodationListing> AccommodationListing{ get; set; }
        public DbSet<AccommodationEstablishment> AccommodationEstablishment { get; set; }
        public DbSet<AccommodationRoom> AccommodationRoom { get; set; }
        public DbSet<AccommodationAmenity> AccommodationAmenity { get; set; }
        public DbSet<AccommodationRoomAmenity> AccommodationRoomAmenity { get; set; }
        public DbSet<Platform> Platform { get; set; }
        public DbSet<AccommodationAvailability> AccommodationAvailability { get; set; }
        public DbSet<AccommodationReview> AccommodationReview { get; set; }


        // relaties, keys, constraints en andere configuraties 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("postgis"); 
            modelBuilder.HasPostgresExtension("timescaledb");

            // primary keys
            modelBuilder.Entity<Platform>().HasKey(p => p.PlatformId);
            modelBuilder.Entity<AccommodationRoomAmenity>() .HasKey(ra => ra.AccommodationRoomAmenityId);
            modelBuilder.Entity<AccommodationRoom>().HasKey(r => r.AccommodationRoomId);
            modelBuilder.Entity<AccommodationReview>().HasKey(r => r.AccommodationReviewId);
            modelBuilder.Entity<AccommodationListing>().HasKey(l => l.AccommodationListingId);
            modelBuilder.Entity<AccommodationEstablishment>().HasKey(e => e.AccommodationEstablishmentId);
            modelBuilder.Entity<AccommodationAmenity>().HasKey(a => a.AccommodationAmenityId);

            // samengestelde primary key voor AccommodationAvailability
            modelBuilder.Entity<AccommodationAvailability>()
            .HasKey(a => new { a.AccommodationListingId, a.AvailabilityDate, a.ScrapingDate });



            // foreign keys en relaties

            // AccommodationListing 0..* - 1 Platform
            modelBuilder.Entity<AccommodationListing>()
                .HasOne(l => l.Platform)
                .WithMany()
                .HasForeignKey(l => l.PlatformId)
                .OnDelete(DeleteBehavior.Restrict);

            // AccommodationListing 1 - 0..* AccommodationAvailability 
            modelBuilder.Entity<AccommodationAvailability>()
                .HasOne(a => a.AccommodationListing)
                .WithMany()
                .HasForeignKey(a => a.AccommodationListingId);

            // AccommodationListing 1 - 0..* AccommodationReview
            modelBuilder.Entity<AccommodationReview>()
                .HasOne(r => r.AccommodationListing)
                .WithMany(l => l.AccommodationReviews)
                .HasForeignKey(r => r.AccommodationListingId);

            // AccommodationListing 1 - 0..* AccommodationAmenity
            modelBuilder.Entity<AccommodationAmenity>()
                .HasOne(a => a.AccommodationListing)
                .WithMany(l => l.AccommodationAmenities)
                .HasForeignKey(a => a.AccommodationListingId);

            // AccommodationListing 1 - 0..* AccommodationRoom
            modelBuilder.Entity<AccommodationRoom>()
                .HasOne(r => r.AccommodationListing)
                .WithMany(l => l.AccommodationRooms)
                .HasForeignKey(r => r.AccommodationListingId);

            // AccommodationRoom 1 - 0..* AccommodationRoomAmenity
            modelBuilder.Entity<AccommodationRoomAmenity>()
                .HasOne(ra => ra.AccommodationRoom)
                .WithMany(r => r.AccommodationRoomAmenities)
                .HasForeignKey(ra => ra.AccommodationRoomId);

            // AccommodationListing 1..* - 0..1 AccommodationEstablishment (applicatie zorgt ervoor dat een Establishment-rij voortkomt uit minimaal 1 listing)
            modelBuilder.Entity<AccommodationListing>()
                .HasOne(l => l.AccommodationEstablishment)
                .WithMany()
                .HasForeignKey(l => l.AccommodationEstablishmentId)
                .OnDelete(DeleteBehavior.SetNull);




            // index om de laatste versie van een listing op te zoeken, niet uniek
            modelBuilder.Entity<AccommodationListing>()
                .HasIndex(l => new { l.PlatformId, l.SourceId });

            // registratienummer uniek 
            modelBuilder.Entity<AccommodationEstablishment>()
                .HasIndex(e => e.RegistrationNumber)
                .IsUnique();

            // coördinaten als PostGIS-punt in WGS84 (breedte-/lengtegraad)
            modelBuilder.Entity<AccommodationListing>()
                .Property(l => l.Coordinates)
                .HasColumnType("geography (point, 4326)");

            // precisie van numerieke velden
            modelBuilder.Entity<AccommodationRoom>(r =>
            {
                r.Property(x => x.TotalPrice).HasPrecision(10, 2);
                r.Property(x => x.TaxAmount).HasPrecision(10, 2);
                r.Property(x => x.RoomSize).HasPrecision(8, 2);
            });
            modelBuilder.Entity<AccommodationReview>()
                .Property(x => x.AverageReviewScore).HasPrecision(4, 2);
            modelBuilder.Entity<AccommodationListing>()
                .Property(x => x.StarClassification).HasPrecision(2, 1);

            // platform entiteit seeden met Airbnb en Booking.com 
            modelBuilder.Entity<Platform>().HasData(
                new Platform { PlatformId = 1, PlatformName = "Booking.com" },
                new Platform { PlatformId = 2, PlatformName = "Airbnb" });

        }
    }
}