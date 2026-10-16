using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class ResultDto
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? BibNumber { get; set; }
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeOnly? FinishTime { get; set; }
    public decimal? DistanceCoveredKM { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CapturedByOrganiserID { get; set; }
    public DateTime CapturedAt { get; set; }
}

// Used for updating a result (PUT)
public class SaveResultDto
{
    public TimeOnly? FinishTime { get; set; }

    [Range(0, 999.99)]
    public decimal? DistanceCoveredKM { get; set; }

    [Range(1, 100000)]
    public int? Position { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;   // Finished, DNF or DNS
}

// Used for creating a result (POST). It is the same as above plus the enrolment ID.
public class CreateResultDto : SaveResultDto
{
    [Required]
    public int? EnrolmentId { get; set; }
}
