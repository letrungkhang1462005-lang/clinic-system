using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Core.Enums;

public enum AppointmentStatus
{
    Pending = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3
}

public enum InvoiceStatus
{
    Unpaid = 0,
    Paid = 1
}

public enum InvoiceItemType
{
    Examination = 0,
    Service = 1,
    Medicine = 2
}
