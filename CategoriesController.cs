using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize(Roles = Roles.Organiser)]   // both endpoints are Organiser-only
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriesController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Updates a category. Only the Organiser who owns the category's event may do this.
    /// </summary>
    /// <param name="id">The category's ID.</param>
    /// <param name="dto">The new category details.</param>
    /// <returns>The updated category.</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(int id, SaveCategoryDto dto)
    {
        // Include the Event so we can check who owns it
        var category = await _context.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryID == id);

        if (category == null)
        {
            return NotFound(new { message = "Category not found." });
        }

        if (category.Event.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only edit categories of events that you created." });
        }

        category.CategoryName = dto.CategoryName.Trim();
        category.DistanceKM = dto.DistanceKM;
        category.MinAge = dto.MinAge;
        category.EntryFee = dto.EntryFee;

        await _context.SaveChangesAsync();

        return Ok(category.ToDto());
    }

    /// <summary>
    /// Deletes a category. Only the Organiser who owns the category's event may do this,
    /// and only while nobody is enrolled in it.
    /// </summary>
    /// <param name="id">The category's ID.</param>
    /// <returns>204 with no body when deleted.</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryID == id);

        if (category == null)
        {
            return NotFound(new { message = "Category not found." });
        }

        if (category.Event.OrganiserID != User.GetUserId())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete categories of events that you created." });
        }

        if (await _context.Enrolments.AnyAsync(e => e.CategoryID == id))
        {
            return Conflict(new { message = "This category has enrolments and cannot be deleted." });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
