using System.Text.Json;
using System.Collections.Concurrent;

using ParkingApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// In-memory data store
var users = new ConcurrentDictionary<int, User>();
var cars = new ConcurrentDictionary<int, Car>();
var parkingSpots = new ConcurrentDictionary<int, ParkingSpot>();

// Seed some initial data
users.TryAdd(1, new User { UserId = 1, FirstName = "John", LastName = "Doe", Email = "john.doe@example.com" });
cars.TryAdd(1, new Car { CarId = 1, LicensePlate = "ABC123", Make = "Toyota", Model = "Camry", OwnerId = 1 });
parkingSpots.TryAdd(1, new ParkingSpot { ParkingSpotId = 1, SpotNumber = "A1", IsAvailable = true });

// Register Car Endpoint
app.MapPost("/register-car", (Car car) =>
    {
        // Generate a unique CarId (for simplicity, increment the last ID)
        car.CarId = cars.Count > 0 ? cars.Keys.Max() + 1 : 1;

        // Add the car to the in-memory data store
        if (!cars.TryAdd(car.CarId, car))
        {
            return Results.BadRequest("Failed to register the car.");
        }

        return Results.Ok($"Car registered successfully with ID: {car.CarId}.");
    })
    .Accepts<Car>("application/json"); // Explicitly specify the request body type

// Begin Parking Endpoint
app.MapGet("/begin-parking", (int carId, int parkingSpotId) =>
{
    if (!cars.TryGetValue(carId, out var car))
    {
        return Results.NotFound("Car not found.");
    }

    if (!parkingSpots.TryGetValue(parkingSpotId, out var parkingSpot))
    {
        return Results.NotFound("Parking spot not found.");
    }

    if (!parkingSpot.IsAvailable)
    {
        return Results.BadRequest("Parking spot is not available.");
    }

    // Update parking spot and car
    parkingSpot.IsAvailable = false;
    parkingSpot.CarId = carId;
    parkingSpot.OccupiedSince = DateTime.UtcNow;

    car.IsParked = true;
    car.ParkingSpotId = parkingSpotId;
    car.ParkingStartTime = DateTime.UtcNow;

    return Results.Ok($"Parking started for Car {car.LicensePlate} at Spot {parkingSpot.SpotNumber}.");
});

// Exit Parking Endpoint
app.MapGet("/exit-parking", (int carId) =>
{
    if (!cars.TryGetValue(carId, out var car))
    {
        return Results.NotFound("Car not found.");
    }

    if (!car.IsParked || car.ParkingSpotId == null)
    {
        return Results.BadRequest("Car is not currently parked.");
    }

    if (!parkingSpots.TryGetValue(car.ParkingSpotId.Value, out var parkingSpot))
    {
        return Results.NotFound("Parking spot not found.");
    }

    // Calculate parking duration and cost
    var startTime = car.ParkingStartTime ?? DateTime.UtcNow;
    var endTime = DateTime.UtcNow;
    var totalCost = CalculateParkingCost(startTime, endTime);

    // Update parking spot and car
    parkingSpot.IsAvailable = true;
    parkingSpot.CarId = null;
    parkingSpot.OccupiedSince = null;

    car.IsParked = false;
    car.ParkingSpotId = null;
    car.ParkingStartTime = null;
    car.ParkingEndTime = endTime;

    return Results.Ok($"Parking ended for Car {car.LicensePlate}. Duration: {(endTime - startTime).TotalHours:F2} hours. Total Cost: {totalCost:C}.");
});

// Get Parking Period Endpoint
app.MapGet("/get-parking-period", (int carId) =>
{
    if (!cars.TryGetValue(carId, out var car))
    {
        return Results.NotFound("Car not found.");
    }

    if (car.ParkingStartTime == null || car.ParkingEndTime == null)
    {
        return Results.BadRequest("No parking period found for this car.");
    }

    var startTime = car.ParkingStartTime.Value;
    var endTime = car.ParkingEndTime.Value;
    var duration = endTime - startTime;

    return Results.Ok(new
    {
        CarId = car.CarId,
        LicensePlate = car.LicensePlate,
        StartTime = startTime,
        EndTime = endTime,
        DurationHours = duration.TotalHours
    });
});

// Get Parking Cost Endpoint
app.MapGet("/get-parking-cost", (int carId) =>
{
    if (!cars.TryGetValue(carId, out var car))
    {
        return Results.NotFound("Car not found.");
    }

    if (car.ParkingStartTime == null || car.ParkingEndTime == null)
    {
        return Results.BadRequest("No parking period found for this car.");
    }

    // Calculate parking cost
    var startTime = car.ParkingStartTime.Value;
    var endTime = car.ParkingEndTime.Value;
    var totalCost = CalculateParkingCost(startTime, endTime);

    return Results.Ok(new
    {
        CarId = car.CarId,
        LicensePlate = car.LicensePlate,
        StartTime = startTime,
        EndTime = endTime,
        TotalCost = totalCost
    });
});

// Get User’s All Registered Details Endpoint
app.MapGet("/get-user-details", (int userId) =>
{
    if (!users.TryGetValue(userId, out var user))
    {
        return Results.NotFound("User not found.");
    }

    // Get all cars registered by the user
    var userCars = cars.Values.Where(c => c.OwnerId == userId).ToList();

    // Get all parking history for the user’s cars
    var parkingHistory = userCars
        .Select(c => new
        {
            CarId = c.CarId,
            LicensePlate = c.LicensePlate,
            ParkingStartTime = c.ParkingStartTime,
            ParkingEndTime = c.ParkingEndTime,
            TotalCost = c.ParkingStartTime != null && c.ParkingEndTime != null
                ? CalculateParkingCost(c.ParkingStartTime.Value, c.ParkingEndTime.Value)
                : 0
        })
        .ToList();

    return Results.Ok(new
    {
        UserId = user.UserId,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        Cars = userCars,
        ParkingHistory = parkingHistory
    });
});


app.Run();

// Helper method to calculate parking cost
decimal CalculateParkingCost(DateTime startTime, DateTime endTime)
{
    decimal totalCost = 0;
    var currentTime = startTime;

    while (currentTime < endTime)
    {
        var nextHour = currentTime.AddHours(1);
        if (nextHour > endTime)
        {
            nextHour = endTime;
        }

        // Check if the current hour is between 8 AM and 6 PM
        if (currentTime.Hour >= 8 && currentTime.Hour < 18)
        {
            totalCost += 14 * (decimal)(nextHour - currentTime).TotalHours;
        }
        else
        {
            totalCost += 6 * (decimal)(nextHour - currentTime).TotalHours;
        }

        currentTime = nextHour;
    }

    return totalCost;
}


