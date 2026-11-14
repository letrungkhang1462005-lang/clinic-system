using Clinic.Core.DTOs;
using Clinic.Core.Enums;

namespace Clinic.Core.Interfaces;

public interface IPatientService
{
    Task<PatientDto> CreateWalkInAsync(CreatePatientRequest request);
    Task<List<PatientDto>> GetListAsync(int userId, string role, string? keyword);
    Task<PatientDto> GetByIdAsync(int id, int userId, string role);
}

public interface IInvoiceService
{
    Task<InvoiceDto> CreateAsync(int receptionistUserId, CreateInvoiceRequest request);
    Task<InvoiceDto> PayAsync(int id, string paymentMethod);
    Task<InvoiceDto> GetByIdAsync(int id, int userId, string role);
    Task<List<InvoiceDto>> GetListAsync(DateOnly? date, InvoiceStatus? status);
    Task<List<InvoiceDto>> GetMyAsync(int userId);
}

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to);
    Task<List<RevenuePointDto>> GetRevenueByDayAsync(DateOnly? from, DateOnly? to);
    Task<List<TopDoctorDto>> GetTopDoctorsAsync(DateOnly? from, DateOnly? to, int limit);
    Task<List<PeakHourDto>> GetPeakHoursAsync(DateOnly? from, DateOnly? to);
}