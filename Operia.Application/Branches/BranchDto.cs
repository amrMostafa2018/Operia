namespace Operia.Application.Branches;

public sealed record BranchDto(
    string Id,
    string Name,
    string Address,
    string PhoneNumber,
    decimal Latitude,
    decimal Longitude,
    string GoogleMapsUrl);
