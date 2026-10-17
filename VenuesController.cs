using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/venues")]
[Authorize]   // must be logged in (either role) for every endpoint in here
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _context;

    public VenuesController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns all venues, so an Organiser can pick one when creating an event.
    /// </summary>
    /// <returns>A list of venues ordered by name.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<VenueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetVenues()
    {
        var venues = await _context.Venues
            .OrderBy(v => v.VenueName)
            .ToListAsync();

        return Ok(venues.Select(ToDto));
    }

    /// <summary>
    /// Creates a new venue. Organisers only.
    /// </summary>
    /// <param name="dto">Venue name, city and province are required. Address and coordinates are optional.</param>
    /// <returns>The newly created venue.</returns>
    [HttpPost]
    [Authorize(Roles = Roles.Organiser)]   // adds the role rule on top of the logged-in rule above
    [ProducesResponseType(typeof(VenueDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateVenue(CreateVenueDto dto)
    {
        var venue = new Venue
        {
            VenueName = dto.VenueName.Trim(),
            AddressLine = dto.AddressLine,
            City = dto.City.Trim(),
            Province = dto.Province.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        _context.Venues.Add(venue);
        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, ToDto(venue));
    }

    private static VenueDto ToDto(Venue venue)
    {
        return new VenueDto
        {
            VenueID = venue.VenueID,
            VenueName = venue.VenueName,
            AddressLine = venue.AddressLine,
            City = venue.City,
            Province = venue.Province,
            Latitude = venue.Latitude,
            Longitude = venue.Longitude
        };
    }
}
