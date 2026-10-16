using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

public enum UserRole
{
    Organiser,
    Participant
}

public enum EventType
{
    Run,
    Walk,
    Cycle
}

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>BCrypt hash. The plain-text password is never stored.</summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Event
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    [Required, MaxLength(300)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Distance in kilometres.</summary>
    public decimal DistanceKm { get; set; }

    public EventType EventType { get; set; }

    public int OrganiserId { get; set; }
    public User Organiser { get; set; } = null!;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Category
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
}

public class Enrolment
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public Result? Result { get; set; }
}

public class Result
{
    public int Id { get; set; }

    public int EnrolmentId { get; set; }
    public Enrolment Enrolment { get; set; } = null!;

    public TimeSpan FinishTime { get; set; }

    public int Position { get; set; }
}
