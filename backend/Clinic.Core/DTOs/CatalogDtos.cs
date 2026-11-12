using System.ComponentModel.DataAnnotations;

namespace Clinic.Core.DTOs;

// ----- Chuyên khoa -----
public record SpecialtyDto(int Id, string Name, string? Description);

public class SpecialtyRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}

// ----- Bác sĩ -----
public record DoctorDto(
    int Id, string FullName, string Email, string? Phone,
    int SpecialtyId, string SpecialtyName,
    string? Degree, int? ExperienceYears, string? Bio,
    decimal ConsultationFee, bool IsActive);

public class CreateDoctorRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Range(1, int.MaxValue)]
    public int SpecialtyId { get; set; }

    [MaxLength(100)]
    public string? Degree { get; set; }

    [Range(0, 80)]
    public int? ExperienceYears { get; set; }

    [MaxLength(1000)]
    public string? Bio { get; set; }

    [Range(0, 100000000)]
    public decimal ConsultationFee { get; set; }
}

public class UpdateDoctorRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Range(1, int.MaxValue)]
    public int SpecialtyId { get; set; }

    [MaxLength(100)]
    public string? Degree { get; set; }

    [Range(0, 80)]
    public int? ExperienceYears { get; set; }

    [MaxLength(1000)]
    public string? Bio { get; set; }

    [Range(0, 100000000)]
    public decimal ConsultationFee { get; set; }

    public bool IsActive { get; set; } = true;
}

// ----- Lịch làm việc và khung giờ -----
public record ScheduleDto(
    int Id, int DoctorId, DayOfWeek DayOfWeek,
    TimeOnly StartTime, TimeOnly EndTime, int SlotMinutes, bool IsActive);

public class ScheduleRequest
{
    [EnumDataType(typeof(DayOfWeek))]
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    [Range(10, 120)]
    public int SlotMinutes { get; set; } = 30;
}

public record SlotDto(TimeOnly StartTime, TimeOnly EndTime, bool IsAvailable);