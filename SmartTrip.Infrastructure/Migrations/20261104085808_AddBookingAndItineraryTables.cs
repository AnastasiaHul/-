using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartTrip.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingAndItineraryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accommodations",
                columns: table => new
                {
                    AccommodationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MaxGuests = table.Column<int>(type: "integer", nullable: false),
                    PricePerNight = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accommodations", x => x.AccommodationId);
                    table.CheckConstraint("CK_Accommodation_MaxGuests", "\"MaxGuests\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ExcursionSessions",
                columns: table => new
                {
                    ExcursionSessionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MeetingAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    PricePerPerson = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcursionSessions", x => x.ExcursionSessionId);
                    table.CheckConstraint("CK_ExcursionSession_Settings", "\"Capacity\" > 0 AND \"DurationMinutes\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ItineraryVisits",
                columns: table => new
                {
                    ItineraryVisitId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripId = table.Column<int>(type: "integer", nullable: false),
                    PlaceId = table.Column<int>(type: "integer", nullable: false),
                    PlaceName = table.Column<string>(type: "text", nullable: false),
                    VisitAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItineraryVisits", x => x.ItineraryVisitId);
                    table.ForeignKey(
                        name: "FK_ItineraryVisits_Places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "Places",
                        principalColumn: "PlaceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItineraryVisits_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RentalCars",
                columns: table => new
                {
                    RentalCarId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PickupAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Transmission = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SeatsCount = table.Column<int>(type: "integer", nullable: false),
                    PricePerDay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentalCars", x => x.RentalCarId);
                    table.CheckConstraint("CK_RentalCar_Seats", "\"SeatsCount\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "TrainDepartures",
                columns: table => new
                {
                    TrainDepartureId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrainNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DepartureCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ArrivalCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DepartureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArrivalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WagonCount = table.Column<int>(type: "integer", nullable: false),
                    SeatsPerWagon = table.Column<int>(type: "integer", nullable: false),
                    TicketPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainDepartures", x => x.TrainDepartureId);
                    table.CheckConstraint("CK_TrainDeparture_Capacity", "\"WagonCount\" > 0 AND \"SeatsPerWagon\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "AccommodationReservations",
                columns: table => new
                {
                    AccommodationReservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripId = table.Column<int>(type: "integer", nullable: false),
                    AccommodationId = table.Column<int>(type: "integer", nullable: false),
                    CheckIn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckOut = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GuestsCount = table.Column<int>(type: "integer", nullable: false),
                    GuestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BookingCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationReservations", x => x.AccommodationReservationId);
                    table.CheckConstraint("CK_AccommodationReservation_Dates", "\"CheckOut\" > \"CheckIn\"");
                    table.CheckConstraint("CK_AccommodationReservation_Guests", "\"GuestsCount\" > 0");
                    table.ForeignKey(
                        name: "FK_AccommodationReservations_Accommodations_AccommodationId",
                        column: x => x.AccommodationId,
                        principalTable: "Accommodations",
                        principalColumn: "AccommodationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccommodationReservations_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExcursionReservations",
                columns: table => new
                {
                    ExcursionReservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripId = table.Column<int>(type: "integer", nullable: false),
                    ExcursionSessionId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantsCount = table.Column<int>(type: "integer", nullable: false),
                    GuestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BookingCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcursionReservations", x => x.ExcursionReservationId);
                    table.CheckConstraint("CK_ExcursionReservation_Participants", "\"ParticipantsCount\" > 0");
                    table.ForeignKey(
                        name: "FK_ExcursionReservations_ExcursionSessions_ExcursionSessionId",
                        column: x => x.ExcursionSessionId,
                        principalTable: "ExcursionSessions",
                        principalColumn: "ExcursionSessionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExcursionReservations_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CarReservations",
                columns: table => new
                {
                    CarReservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripId = table.Column<int>(type: "integer", nullable: false),
                    RentalCarId = table.Column<int>(type: "integer", nullable: false),
                    PickupDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DriverName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DriverAge = table.Column<int>(type: "integer", nullable: false),
                    BookingCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarReservations", x => x.CarReservationId);
                    table.CheckConstraint("CK_CarReservation_Dates", "\"ReturnDate\" > \"PickupDate\"");
                    table.CheckConstraint("CK_CarReservation_DriverAge", "\"DriverAge\" >= 21 AND \"DriverAge\" <= 100");
                    table.ForeignKey(
                        name: "FK_CarReservations_RentalCars_RentalCarId",
                        column: x => x.RentalCarId,
                        principalTable: "RentalCars",
                        principalColumn: "RentalCarId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CarReservations_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainReservations",
                columns: table => new
                {
                    TrainReservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripId = table.Column<int>(type: "integer", nullable: false),
                    TrainDepartureId = table.Column<int>(type: "integer", nullable: false),
                    WagonNumber = table.Column<int>(type: "integer", nullable: false),
                    SeatNumber = table.Column<int>(type: "integer", nullable: false),
                    PassengerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BookingCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainReservations", x => x.TrainReservationId);
                    table.CheckConstraint("CK_TrainReservation_SeatNumbers", "\"WagonNumber\" > 0 AND \"SeatNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_TrainReservations_TrainDepartures_TrainDepartureId",
                        column: x => x.TrainDepartureId,
                        principalTable: "TrainDepartures",
                        principalColumn: "TrainDepartureId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainReservations_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationReservations_AccommodationId_Status_CheckIn_Ch~",
                table: "AccommodationReservations",
                columns: new[] { "AccommodationId", "Status", "CheckIn", "CheckOut" });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationReservations_BookingCode",
                table: "AccommodationReservations",
                column: "BookingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationReservations_TripId",
                table: "AccommodationReservations",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Accommodations_City_IsActive",
                table: "Accommodations",
                columns: new[] { "City", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CarReservations_BookingCode",
                table: "CarReservations",
                column: "BookingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarReservations_RentalCarId_Status_PickupDate_ReturnDate",
                table: "CarReservations",
                columns: new[] { "RentalCarId", "Status", "PickupDate", "ReturnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CarReservations_TripId",
                table: "CarReservations",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcursionReservations_BookingCode",
                table: "ExcursionReservations",
                column: "BookingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExcursionReservations_ExcursionSessionId_Status",
                table: "ExcursionReservations",
                columns: new[] { "ExcursionSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExcursionReservations_TripId",
                table: "ExcursionReservations",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcursionSessions_City_StartsAt_IsActive",
                table: "ExcursionSessions",
                columns: new[] { "City", "StartsAt", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ItineraryVisits_PlaceId",
                table: "ItineraryVisits",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ItineraryVisits_TripId_VisitAt",
                table: "ItineraryVisits",
                columns: new[] { "TripId", "VisitAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalCars_City_IsActive",
                table: "RentalCars",
                columns: new[] { "City", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainDepartures_DepartureCity_ArrivalCity_DepartureAt",
                table: "TrainDepartures",
                columns: new[] { "DepartureCity", "ArrivalCity", "DepartureAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainReservations_BookingCode",
                table: "TrainReservations",
                column: "BookingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainReservations_TripId",
                table: "TrainReservations",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "UX_TrainReservation_Seat",
                table: "TrainReservations",
                columns: new[] { "TrainDepartureId", "WagonNumber", "SeatNumber" },
                unique: true,
                filter: "\"Status\" = 'Confirmed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccommodationReservations");

            migrationBuilder.DropTable(
                name: "CarReservations");

            migrationBuilder.DropTable(
                name: "ExcursionReservations");

            migrationBuilder.DropTable(
                name: "ItineraryVisits");

            migrationBuilder.DropTable(
                name: "TrainReservations");

            migrationBuilder.DropTable(
                name: "Accommodations");

            migrationBuilder.DropTable(
                name: "RentalCars");

            migrationBuilder.DropTable(
                name: "ExcursionSessions");

            migrationBuilder.DropTable(
                name: "TrainDepartures");
        }
    }
}
