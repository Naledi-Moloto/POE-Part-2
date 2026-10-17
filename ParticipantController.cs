using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/participants")]
[Authorize(Roles = Roles.Participant)]   // every endpoint in this controller is Participant-only
public class ParticipantsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ParticipantsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the profile of the currently logged-in Participant.
    /// </summary>
    /// <returns>The participant's profile (the password hash is never included).</returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ParticipantProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile()
    {
        var participant = await _context.Participants.FindAsync(User.GetUserId());

        if (participant == null)
        {
            return NotFound(new { message = "Participant profile not found." });
        }

        return Ok(ToDto(participant));
    }

    /// <summary>
    /// Updates the full name, phone number and date of birth of the currently logged-in Participant.
    /// </summary>
    /// <param name="dto">The new full name, optional phone number and optional date of birth.</param>
    /// <returns>The updated profile.</returns>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ParticipantProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile(UpdateParticipantProfileDto dto)
    {
        if (dto.DateOfBirth.HasValue && dto.DateOfBirth.Value > DateOnly.FromDateTime(DateTime.Today))
        {
            return BadRequest(new { message = "Date of birth cannot be in the future." });
        }

        var participant = await _context.Participants.FindAsync(User.GetUserId());

        if (participant == null)
        {
            return NotFound(new { message = "Participant profile not found." });
        }

        participant.FullName = dto.FullName.Trim();
        participant.PhoneNumber = dto.PhoneNumber;
        participant.DateOfBirth = dto.DateOfBirth;
        await _context.SaveChangesAsync();

        return Ok(ToDto(participant));
    }

    private static ParticipantProfileDto ToDto(Participant participant)
    {
        return new ParticipantProfileDto
        {
            ParticipantID = participant.ParticipantID,
            FullName = participant.FullName,
            Email = participant.Email,
            PhoneNumber = participant.PhoneNumber,
            DateOfBirth = participant.DateOfBirth,
            CreatedAt = participant.CreatedAt
        };
    }
}