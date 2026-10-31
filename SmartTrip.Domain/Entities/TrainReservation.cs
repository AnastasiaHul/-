using System;

namespace SmartTrip.Domain.Entities;

public sealed class TrainReservation
{
    public int TrainReservationId { get; set; }

    // Подорож, до якої належить бронювання.
    public int TripId { get; set; }

    // Вибраний рейс поїзда.
    public int TrainDepartureId { get; set; }

    public int WagonNumber { get; set; }

    public int SeatNumber { get; set; }

    public string PassengerName { get; set; } = string.Empty;

    // Унікальний код підтвердження.
    public string BookingCode { get; set; } = string.Empty;

    // Ціна на момент оформлення бронювання.
    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = "Confirmed";
}