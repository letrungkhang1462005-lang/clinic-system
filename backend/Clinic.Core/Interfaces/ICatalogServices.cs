using Clinic.Core.DTOs;

namespace Clinic.Core.Interfaces;

public interface ISpecialtyService
{
    Task<List<SpecialtyDto>> GetAllAsync();
    Task<SpecialtyDto> CreateAsync(SpecialtyRequest request);
    Task UpdateAsync(int id, SpecialtyRequest request);
    Task DeleteAsync(int id);
}

public interface IDoctorService
{
    Task<List<DoctorDto>> GetListAsync(int? specialtyId, string? keyword);
    Task<DoctorDto> GetByIdAsync(int id);
    Task<DoctorDto> CreateAsync(CreateDoctorRequest request);
    Task UpdateAsync(int id, UpdateDoctorRequest request);
    Task DeactivateAsync(int id);
}

public interface IScheduleService
{
    Task<List<ScheduleDto>> GetByDoctorAsync(int doctorId);
    Task<ScheduleDto> AddAsync(int doctorId, ScheduleRequest request);
    Task UpdateAsync(int scheduleId, ScheduleRequest request);
    Task DeleteAsync(int scheduleId);
    Task<List<SlotDto>> GetSlotsAsync(int doctorId, DateOnly date);
}