using System.ComponentModel.DataAnnotations;
using Clinic.Core.Enums;

namespace Clinic.Core.DTOs;

// ----- Bệnh nhân -----
public class CreatePatientRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string Phone { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }
}

public record PatientDto(
    int Id, string FullName, string? Phone, string? Email,
    DateOnly? DateOfBirth, string? Gender, string? Address, bool HasAccount);

// ----- Hóa đơn -----
public class InvoiceServiceItem
{
    [Range(1, int.MaxValue)]
    public int ServiceId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; } = 1;
}

public class CreateInvoiceRequest
{
    [Range(1, int.MaxValue)]
    public int AppointmentId { get; set; }

    public List<InvoiceServiceItem> Services { get; set; } = new();
}

public class PayInvoiceRequest
{
    [Required, MaxLength(30)]
    public string PaymentMethod { get; set; } = string.Empty;
}

public record InvoiceItemDto(
    int Id, InvoiceItemType ItemType, string ItemTypeName,
    string Description, int Quantity, decimal UnitPrice, decimal LineTotal);

public record InvoiceDto(
    int Id, int AppointmentId, DateOnly AppointmentDate,
    int PatientId, string PatientName, string DoctorName,
    decimal TotalAmount, InvoiceStatus Status, string StatusName,
    string? PaymentMethod, DateTime? PaidAt, DateTime CreatedAt, string CreatedByName,
    List<InvoiceItemDto> Items);

// ----- Dashboard -----
public record DashboardSummaryDto(
    DateOnly From, DateOnly To,
    decimal TotalRevenue, int PaidInvoices, decimal UnpaidAmount,
    int TotalAppointments, int CompletedAppointments, int CancelledAppointments,
    double CancelRate, int NewPatients);

public record RevenuePointDto(DateOnly Date, decimal Revenue, int Invoices);

public record TopDoctorDto(int DoctorId, string DoctorName, string SpecialtyName, int Appointments, int Completed);

public record PeakHourDto(int Hour, int Appointments);