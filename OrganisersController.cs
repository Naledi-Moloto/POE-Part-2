using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/organisers")]
[Authorize(Roles = Roles.Organiser)]   // every endpoint in this controller is Organiser-only
public class OrganisersController : ControllerBase
{
    private readonly AppDbContext _context;

    public OrganisersController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the profile of the currently logged-in Organiser.
    /// </summary>
    /// <returns>The organiser's profile (the password hash is never included).</returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(OrganiserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile()
    {
        var organiser = await _context.Organisers.FindAsync(User.GetUserId());

        if (organiser == null)
        {
            return NotFound(new { message = "Organiser profile not found." });
        }

        return Ok(ToDto(organiser));
    }

    /// <summary>
    /// Updates the full name and phone number of the currently logged-in Organiser.
    /// </summary>
    /// <param name="dto">The new full name and optional phone number.</param>
    /// <returns>The updated profile.</returns>
    [HttpPut("me")]
    [ProducesResponseType(typeof(OrganiserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile(UpdateOrganiserProfileDto dto)
    {
        var organiser = await _context.Organisers.FindAsync(User.GetUserId());

        if (organiser == null)
        {
            return NotFound(new { message = "Organiser profile not found." });
        }

        organiser.FullName = dto.FullName.Trim();
        organiser.PhoneNumber = dto.PhoneNumber;
        await _context.SaveChangesAsync();

        return Ok(ToDto(organiser));
    }

    // Converts the database entity into the safe DTO we send back
    private static OrganiserProfileDto ToDto(Organiser organiser)
    {
        return new OrganiserProfileDto
        {
            OrganiserID = organiser.OrganiserID,
            FullName = organiser.FullName,
            Email = organiser.Email,
            PhoneNumber = organiser.PhoneNumber,
            CreatedAt = organiser.CreatedAt
        };
    }
}
