using System;

namespace Reservas.Business.Exceptions
{
    /// <summary>
    /// Excepción para conflictos de negocio (409 Conflict), por ejemplo transiciones de estado inválidas.
    /// </summary>
    public class ConflictException : Exception
    {
        public ConflictException() : base() { }

        public ConflictException(string message) : base(message) { }

        public ConflictException(string message, Exception innerException) : base(message, innerException) { }
    }
}

