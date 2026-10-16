using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class VenueDto
{
    public int VenueID { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string? AddressLine { get; set; }
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class CreateVenueDto
{
    [Required, MaxLength(150)]
    public string VenueName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine { get; set; }

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Province { get; set; } = string.Empty;

    [Range(-90.0, 90.0)]
    public decimal? Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public decimal? Longitude { get; set; }
}