using System;

namespace Reservas.Business.Exceptions
{
    public class ForbiddenBusinessException : Exception
    {
        public string? CodigoError { get; set; }

        public ForbiddenBusinessException() : base() { }

        public ForbiddenBusinessException(string message) : base(message) { }

        public ForbiddenBusinessException(string codigoError, string message) : base(message)
        {
            CodigoError = codigoError;
        }

        public ForbiddenBusinessException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
