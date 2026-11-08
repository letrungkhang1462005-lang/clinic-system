using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Clinic.Core.Enums;

namespace Clinic.Core.Entities;

public class Invoice
{
    public int Id { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;

    public ICollection<InvoiceDetail> Details { get; set; } = new List<InvoiceDetail>();
}
