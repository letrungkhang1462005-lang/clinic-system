using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Core.Entities;

public class DoctorSchedule
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int SlotMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;

    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
}
