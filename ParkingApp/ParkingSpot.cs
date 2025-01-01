namespace ParkingApp;

public class ParkingSpot
{
    public int ParkingSpotId { get; set; }
    public string SpotNumber { get; set; }
    public bool IsAvailable { get; set; }
    public int? CarId { get; set; }
    public DateTime? OccupiedSince { get; set; }
}