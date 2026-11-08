using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Core.Entities;

public class PrescriptionDetail
{
    public int Id { get; set; }
    public int Quantity { get; set; }
    public string Dosage { get; set; } = string.Empty;
    public int DurationDays { get; set; }

    public int PrescriptionId { get; set; }
    public Prescription Prescription { get; set; } = null!;

    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;
}
