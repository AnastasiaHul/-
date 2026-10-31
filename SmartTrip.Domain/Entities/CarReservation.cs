namespace SmartTrip.Domain.Entities;

public sealed class CarReservation
{
    public int CarReservationId { get; set; }

    public int TripId { get; set; }

    public int RentalCarId { get; set; }

    public DateTime PickupDate { get; set; }

    public DateTime ReturnDate { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public int DriverAge { get; set; }

    public string BookingCode { get; set; } = string.Empty;

    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = "Confirmed";
}