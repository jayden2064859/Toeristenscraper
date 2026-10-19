using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Toeristenscraper.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .Annotation("Npgsql:PostgresExtension:timescaledb", ",,");

            migrationBuilder.CreateTable(
                name: "AccommodationEstablishment",
                columns: table => new
                {
                    AccommodationEstablishmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegistrationNumber = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationEstablishment", x => x.AccommodationEstablishmentId);
                });

            migrationBuilder.CreateTable(
                name: "Platform",
                columns: table => new
                {
                    PlatformId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlatformName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platform", x => x.PlatformId);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationListing",
                columns: table => new
                {
                    AccommodationListingId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccommodationEstablishmentId = table.Column<int>(type: "integer", nullable: true),
                    PlatformId = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    KVKNumber = table.Column<string>(type: "text", nullable: true),
                    AccommodationType = table.Column<string>(type: "text", nullable: true),
                    Coordinates = table.Column<Point>(type: "geography (point, 4326)", nullable: true),
                    IsExactLocation = table.Column<bool>(type: "boolean", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    Municipality = table.Column<string>(type: "text", nullable: true),
                    Province = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    StarClassification = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: true),
                    Superhost = table.Column<bool>(type: "boolean", nullable: true),
                    URL = table.Column<string>(type: "text", nullable: true),
                    ScrapingDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationListing", x => x.AccommodationListingId);
                    table.ForeignKey(
                        name: "FK_AccommodationListing_AccommodationEstablishment_Accommodati~",
                        column: x => x.AccommodationEstablishmentId,
                        principalTable: "AccommodationEstablishment",
                        principalColumn: "AccommodationEstablishmentId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccommodationListing_Platform_PlatformId",
                        column: x => x.PlatformId,
                        principalTable: "Platform",
                        principalColumn: "PlatformId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationAmenity",
                columns: table => new
                {
                    AccommodationAmenityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccommodationListingId = table.Column<int>(type: "integer", nullable: false),
                    AmenityValue = table.Column<string>(type: "text", nullable: false),
                    ScrapingDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationAmenity", x => x.AccommodationAmenityId);
                    table.ForeignKey(
                        name: "FK_AccommodationAmenity_AccommodationListing_AccommodationList~",
                        column: x => x.AccommodationListingId,
                        principalTable: "AccommodationListing",
                        principalColumn: "AccommodationListingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationAvailability",
                columns: table => new
                {
                    AccommodationListingId = table.Column<int>(type: "integer", nullable: false),
                    AvailabilityDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ScrapingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    MinNights = table.Column<int>(type: "integer", nullable: false),
                    MaxNights = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationAvailability", x => new { x.AccommodationListingId, x.AvailabilityDate, x.ScrapingDate });
                    table.ForeignKey(
                        name: "FK_AccommodationAvailability_AccommodationListing_Accommodatio~",
                        column: x => x.AccommodationListingId,
                        principalTable: "AccommodationListing",
                        principalColumn: "AccommodationListingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationReview",
                columns: table => new
                {
                    AccommodationReviewId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccommodationListingId = table.Column<int>(type: "integer", nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: true),
                    AverageReviewScore = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    ScrapingDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationReview", x => x.AccommodationReviewId);
                    table.ForeignKey(
                        name: "FK_AccommodationReview_AccommodationListing_AccommodationListi~",
                        column: x => x.AccommodationListingId,
                        principalTable: "AccommodationListing",
                        principalColumn: "AccommodationListingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationRoom",
                columns: table => new
                {
                    AccommodationRoomId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccommodationListingId = table.Column<int>(type: "integer", nullable: false),
                    SourceRoomId = table.Column<string>(type: "text", nullable: true),
                    RoomType = table.Column<string>(type: "text", nullable: true),
                    RoomSize = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: true),
                    BedCount = table.Column<int>(type: "integer", nullable: true),
                    BedType = table.Column<string>(type: "text", nullable: true),
                    TotalPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    TaxIncluded = table.Column<bool>(type: "boolean", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AdultCount = table.Column<int>(type: "integer", nullable: false),
                    ChildCount = table.Column<int>(type: "integer", nullable: false),
                    PetCount = table.Column<int>(type: "integer", nullable: false),
                    ScrapingDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationRoom", x => x.AccommodationRoomId);
                    table.ForeignKey(
                        name: "FK_AccommodationRoom_AccommodationListing_AccommodationListing~",
                        column: x => x.AccommodationListingId,
                        principalTable: "AccommodationListing",
                        principalColumn: "AccommodationListingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationRoomAmenity",
                columns: table => new
                {
                    AccommodationRoomAmenityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccommodationRoomId = table.Column<int>(type: "integer", nullable: false),
                    AmenityValue = table.Column<string>(type: "text", nullable: false),
                    ScrapingDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationRoomAmenity", x => x.AccommodationRoomAmenityId);
                    table.ForeignKey(
                        name: "FK_AccommodationRoomAmenity_AccommodationRoom_AccommodationRoo~",
                        column: x => x.AccommodationRoomId,
                        principalTable: "AccommodationRoom",
                        principalColumn: "AccommodationRoomId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Platform",
                columns: new[] { "PlatformId", "PlatformName" },
                values: new object[,]
                {
                    { 1, "Booking.com" },
                    { 2, "Airbnb" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationAmenity_AccommodationListingId",
                table: "AccommodationAmenity",
                column: "AccommodationListingId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationEstablishment_RegistrationNumber",
                table: "AccommodationEstablishment",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationListing_AccommodationEstablishmentId",
                table: "AccommodationListing",
                column: "AccommodationEstablishmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationListing_PlatformId_SourceId",
                table: "AccommodationListing",
                columns: new[] { "PlatformId", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationReview_AccommodationListingId",
                table: "AccommodationReview",
                column: "AccommodationListingId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationRoom_AccommodationListingId",
                table: "AccommodationRoom",
                column: "AccommodationListingId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationRoomAmenity_AccommodationRoomId",
                table: "AccommodationRoomAmenity",
                column: "AccommodationRoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccommodationAmenity");

            migrationBuilder.DropTable(
                name: "AccommodationAvailability");

            migrationBuilder.DropTable(
                name: "AccommodationReview");

            migrationBuilder.DropTable(
                name: "AccommodationRoomAmenity");

            migrationBuilder.DropTable(
                name: "AccommodationRoom");

            migrationBuilder.DropTable(
                name: "AccommodationListing");

            migrationBuilder.DropTable(
                name: "AccommodationEstablishment");

            migrationBuilder.DropTable(
                name: "Platform");
        }
    }
}
