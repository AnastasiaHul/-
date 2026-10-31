namespace SmartTrip.Domain.Entities;

public sealed class RentalCar
{
    public int RentalCarId { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string PickupAddress { get; set; } = string.Empty;

    public string Transmission { get; set; } = string.Empty;

    public int SeatsCount { get; set; }

    public decimal PricePerDay { get; set; }

    public bool IsActive { get; set; } = true;
}