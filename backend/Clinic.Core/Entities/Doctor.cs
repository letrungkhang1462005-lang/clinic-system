namespace Clinic.Core.Entities;

public class Doctor
{
    public int Id { get; set; }
    public string? Degree { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public decimal ConsultationFee { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;

    public ICollection<DoctorSchedule> Schedules { get; set; } = new List<DoctorSchedule>();
}