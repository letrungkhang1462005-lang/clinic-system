using Clinic.Core.DTOs;

namespace Clinic.Core.Interfaces;

public interface IMedicineService
{
    Task<List<MedicineDto>> GetListAsync(string? keyword, bool includeInactive);
    Task<MedicineDto> CreateAsync(MedicineRequest request);
    Task UpdateAsync(int id, MedicineRequest request);
}

public interface IServiceItemService
{
    Task<List<ServiceDto>> GetListAsync(bool includeInactive);
    Task<ServiceDto> CreateAsync(ServiceRequest request);
    Task UpdateAsync(int id, ServiceRequest request);
}

public interface IExaminationService
{
    Task<MedicalRecordDto> CreateRecordAsync(int doctorUserId, CreateMedicalRecordRequest request);
    Task<MedicalRecordDto> UpdateRecordAsync(int id, int doctorUserId, UpdateMedicalRecordRequest request);
    Task<MedicalRecordDto> GetRecordAsync(int id, int userId, string role);
    Task<PrescriptionDto> CreatePrescriptionAsync(int doctorUserId, CreatePrescriptionRequest request);
    Task<PrescriptionDto> GetPrescriptionAsync(int id, int userId, string role);
    Task<List<MedicalRecordDto>> GetHistoryAsync(int patientId, int userId, string role);
    Task<List<MedicalRecordDto>> GetMyHistoryAsync(int userId);
}