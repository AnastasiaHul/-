namespace SmartTrip.Domain.Entities;

public sealed class ExcursionReservation
{
    public int ExcursionReservationId { get; set; }

    public int TripId { get; set; }

    public int ExcursionSessionId { get; set; }

    public int ParticipantsCount { get; set; }

    public string GuestName { get; set; } = string.Empty;

    public string BookingCode { get; set; } = string.Empty;

    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = "Confirmed";
}