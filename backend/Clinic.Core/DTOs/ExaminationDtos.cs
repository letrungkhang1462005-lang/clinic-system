using System.ComponentModel.DataAnnotations;

namespace Clinic.Core.DTOs;

// ----- Thuốc -----
public record MedicineDto(int Id, string Name, string Unit, decimal Price, string? Description, bool IsActive);

public class MedicineRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Unit { get; set; } = string.Empty;

    [Range(0, 100000000)]
    public decimal Price { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

// ----- Dịch vụ -----
public record ServiceDto(int Id, string Name, decimal Price, string? Description, bool IsActive);

public class ServiceRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100000000)]
    public decimal Price { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

// ----- Bệnh án -----
public class CreateMedicalRecordRequest
{
    [Range(1, int.MaxValue)]
    public int AppointmentId { get; set; }

    [MaxLength(1000)]
    public string? Symptoms { get; set; }

    [Required, MaxLength(1000)]
    public string Diagnosis { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateOnly? FollowUpDate { get; set; }
}

public class UpdateMedicalRecordRequest
{
    [MaxLength(1000)]
    public string? Symptoms { get; set; }

    [Required, MaxLength(1000)]
    public string Diagnosis { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateOnly? FollowUpDate { get; set; }
}

// ----- Đơn thuốc -----
public class PrescriptionItemRequest
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }

    [Required, MaxLength(200)]
    public string Dosage { get; set; } = string.Empty;

    [Range(1, 365)]
    public int DurationDays { get; set; }
}

public class CreatePrescriptionRequest
{
    [Range(1, int.MaxValue)]
    public int MedicalRecordId { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    [Required, MinLength(1)]
    public List<PrescriptionItemRequest> Details { get; set; } = new();
}

public record PrescriptionItemDto(
    int Id, int MedicineId, string MedicineName, string Unit,
    int Quantity, string Dosage, int DurationDays);

public record PrescriptionDto(
    int Id, int MedicalRecordId, string? Note, DateTime CreatedAt,
    List<PrescriptionItemDto> Items);

public record MedicalRecordDto(
    int Id, int AppointmentId, DateOnly AppointmentDate,
    int PatientId, string PatientName,
    int DoctorId, string DoctorName,
    string? Symptoms, string Diagnosis, string? Notes, DateOnly? FollowUpDate,
    DateTime CreatedAt, PrescriptionDto? Prescription);