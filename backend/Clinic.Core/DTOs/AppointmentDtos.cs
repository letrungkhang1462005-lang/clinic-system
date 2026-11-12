using System.ComponentModel.DataAnnotations;
using Clinic.Core.Enums;

namespace Clinic.Core.DTOs;

public class CreateAppointmentRequest
{
    [Range(1, int.MaxValue)]
    public int DoctorId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    // Chỉ lễ tân dùng khi đặt hộ bệnh nhân. Bệnh nhân tự đặt thì bỏ qua trường này.
    public int? PatientId { get; set; }
}

public class CancelAppointmentRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public record AppointmentDto(
    int Id,
    int PatientId, string PatientName,
    int DoctorId, string DoctorName, string SpecialtyName,
    DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    AppointmentStatus Status, string StatusName,
    int? QueueNumber, string? Reason, string? CancelReason,
    DateTime CreatedAt);