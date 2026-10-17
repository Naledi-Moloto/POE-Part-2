using System.Linq.Expressions;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Helpers;

public static class Mappers
{
    // Lets us write category.ToDto() anywhere
    public static CategoryDto ToDto(this Category category)
    {
        return new CategoryDto
        {
            CategoryID = category.CategoryID,
            EventID = category.EventID,
            CategoryName = category.CategoryName,
            DistanceKM = category.DistanceKM,
            MinAge = category.MinAge,
            EntryFee = category.EntryFee
        };
    }

    // Shared "recipes" that EF Core turns into SQL (including the joins).
    // Several controllers use them, so they live here instead of being repeated.
    public static readonly Expression<Func<Enrolment, EnrolmentDto>> ToEnrolmentDto = e => new EnrolmentDto
    {
        EnrolmentID = e.EnrolmentID,
        ParticipantID = e.ParticipantID,
        ParticipantName = e.Participant.FullName,
        CategoryID = e.CategoryID,
        CategoryName = e.Category.CategoryName,
        EventID = e.Category.EventID,
        EventName = e.Category.Event.EventName,
        EventDate = e.Category.Event.EventDate,
        DistanceKM = e.Category.DistanceKM,
        EnrolmentDate = e.EnrolmentDate,
        BibNumber = e.BibNumber,
        Status = e.Status
    };

    public static readonly Expression<Func<Result, ResultDto>> ToResultDto = r => new ResultDto
    {
        ResultID = r.ResultID,
        EnrolmentID = r.EnrolmentID,
        ParticipantID = r.Enrolment.ParticipantID,
        ParticipantName = r.Enrolment.Participant.FullName,
        BibNumber = r.Enrolment.BibNumber,
        EventID = r.Enrolment.Category.EventID,
        EventName = r.Enrolment.Category.Event.EventName,
        CategoryName = r.Enrolment.Category.CategoryName,
        FinishTime = r.FinishTime,
        DistanceCoveredKM = r.DistanceCoveredKM,
        Position = r.Position,
        Status = r.Status,
        CapturedByOrganiserID = r.CapturedByOrganiserID,
        CapturedAt = r.CapturedAt
    };
}

