using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Core.Entities;

public class MedicalRecord
{
    public int Id { get; set; }
    public string? Symptoms { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public Prescription? Prescription { get; set; }
}