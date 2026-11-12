using Clinic.Core.DTOs;
using Clinic.Core.Enums;

namespace Clinic.Core.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentDto> CreateAsync(int userId, string role, CreateAppointmentRequest request);
    Task<List<AppointmentDto>> GetMyAsync(int userId, AppointmentStatus? status);
    Task<List<AppointmentDto>> GetListAsync(int userId, string role, DateOnly? date, int? doctorId, AppointmentStatus? status);
    Task<AppointmentDto> GetByIdAsync(int id, int userId, string role);
    Task<AppointmentDto> ConfirmAsync(int id);
    Task<AppointmentDto> CheckInAsync(int id);
    Task<AppointmentDto> CancelAsync(int id, int userId, string role, string? reason);
}