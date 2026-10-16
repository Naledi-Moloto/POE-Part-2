using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

// What the API sends back. It shows the link between participant, event and category.
public class EnrolmentDto
{
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public decimal DistanceKM { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string? BibNumber { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateEnrolmentDto
{
    [Required]
    public int? CategoryId { get; set; }
}