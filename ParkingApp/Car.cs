public class Car
{
    public int CarId { get; set; }
    public string LicensePlate { get; set; }
    public string Make { get; set; }
    public string Model { get; set; }
    public int OwnerId { get; set; }
    public bool IsParked { get; set; }
    public int? ParkingSpotId { get; set; }
    public DateTime? ParkingStartTime { get; set; }
    public DateTime? ParkingEndTime { get; set; }
}