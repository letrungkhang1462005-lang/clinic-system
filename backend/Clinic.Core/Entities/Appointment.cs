using Clinic.Core.Enums;

namespace Clinic.Core.Entities;

public class Appointment
{
    public int Id { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public int? QueueNumber { get; set; }
    public string? Reason { get; set; }
    public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public MedicalRecord? MedicalRecord { get; set; }
    public Invoice? Invoice { get; set; }
}