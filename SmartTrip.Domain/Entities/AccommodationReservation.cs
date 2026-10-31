namespace SmartTrip.Domain.Entities;

public sealed class AccommodationReservation
{
    public int AccommodationReservationId { get; set; }

    public int TripId { get; set; }

    public int AccommodationId { get; set; }

    public DateTime CheckIn { get; set; }

    public DateTime CheckOut { get; set; }

    public int GuestsCount { get; set; }

    public string GuestName { get; set; } = string.Empty;

    public string BookingCode { get; set; } = string.Empty;

    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = "Confirmed";
}