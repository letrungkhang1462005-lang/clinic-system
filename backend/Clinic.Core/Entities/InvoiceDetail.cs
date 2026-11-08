using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Clinic.Core.Enums;

namespace Clinic.Core.Entities;

public class InvoiceDetail
{
    public int Id { get; set; }
    public InvoiceItemType ItemType { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public int? ServiceId { get; set; }
    public Service? Service { get; set; }

    public int? MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
}
