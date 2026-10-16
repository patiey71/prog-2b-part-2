using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------- Requests ----------

public class RegisterDto
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;

    /// <summary>"Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }
    [StringLength(30)] public string? PhoneNumber { get; set; }

    /// <summary>Required for Organiser sign-up when an organiser code is configured on the server.</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileDto
{
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    [StringLength(30)] public string? PhoneNumber { get; set; }
}

public class EventUpsertDto
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
    [Required] public DateTime? Date { get; set; }
    [Required, StringLength(300)] public string Location { get; set; } = string.Empty;
    [Range(0.01, 1000)] public decimal DistanceKm { get; set; }
    [Required] public EventType? EventType { get; set; }
}

public class CategoryCreateDto
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
}

public class EnrolDto
{
    [Required] public int? CategoryId { get; set; }
}

public class ResultUpsertDto
{
    [Required] public int? EnrolmentId { get; set; }

    /// <summary>Finish time as hh:mm:ss, e.g. "00:45:30".</summary>
    [Required] public TimeSpan? FinishTime { get; set; }

    [Range(1, 100000)] public int Position { get; set; }
}

// ---------- Responses ----------

public record ProfileResponse(
    int Id, string FirstName, string LastName, string Email,
    string Role, DateTime? DateOfBirth, string? PhoneNumber);

public record EventResponse(
    int Id, string Name, string Description, DateTime Date, string Location,
    decimal DistanceKm, EventType EventType, int OrganiserId, string OrganiserName);

public record CategoryResponse(int Id, int EventId, string Name);

public record EnrolmentResponse(
    int Id, int EventId, string EventName, int ParticipantId, string ParticipantName,
    int CategoryId, string CategoryName, DateTime EnrolledAt);

public record ResultResponse(
    int Id, int EnrolmentId, int EventId, string EventName, int ParticipantId,
    string ParticipantName, string CategoryName, TimeSpan FinishTime, int Position);
