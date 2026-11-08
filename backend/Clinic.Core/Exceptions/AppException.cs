using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Core.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public AppException(int statusCode, string code, string message) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
