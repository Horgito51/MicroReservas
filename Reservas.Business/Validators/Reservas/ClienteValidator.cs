using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Reservas.Business.DTOs.Reservas;
using Reservas.Business.Exceptions;

namespace Reservas.Business.Validators.Reservas
{
    public static class ClienteValidator
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex Digits10Regex = new(@"^\d{10}$", RegexOptions.Compiled);
        private static readonly Regex IdentificacionRegex = new(@"^\d+$", RegexOptions.Compiled);

        public static void Validate(ClienteDTO cliente)
        {
            if (cliente == null)
                throw new ValidationException("CLI-001", "El cliente no puede ser nulo.");

            var errors = new Dictionary<string, string[]>();
            var tipo = (cliente.TipoIdentificacion ?? string.Empty).Trim().ToUpperInvariant();
            var identificacion = (cliente.NumeroIdentificacion ?? string.Empty).Trim();
            var correo = (cliente.Correo ?? string.Empty).Trim();
            var telefono = OnlyDigits(cliente.Telefono);

            if (string.IsNullOrWhiteSpace(tipo))
                errors["TipoIdentificacion"] = new[] { "El tipo de identificacion es obligatorio." };

            if (string.IsNullOrWhiteSpace(identificacion))
                errors["NumeroIdentificacion"] = new[] { "El numero de identificacion es obligatorio." };
            else if (!IdentificacionRegex.IsMatch(identificacion))
                errors["NumeroIdentificacion"] = new[] { "La identificacion solo debe contener numeros." };

            if (string.IsNullOrWhiteSpace(cliente.Nombres) && string.IsNullOrWhiteSpace(cliente.RazonSocial))
                errors["Nombres"] = new[] { "Los nombres o razon social son obligatorios." };

            if (string.IsNullOrWhiteSpace(correo))
                errors["Correo"] = new[] { "El correo electronico es obligatorio." };
            else if (!EmailRegex.IsMatch(correo))
                errors["Correo"] = new[] { "El correo electronico no tiene un formato valido." };

            if (string.IsNullOrWhiteSpace(telefono))
                errors["Telefono"] = new[] { "El telefono es obligatorio." };
            else if (!Digits10Regex.IsMatch(telefono))
                errors["Telefono"] = new[] { "El telefono debe contener exactamente 10 digitos." };

            if (errors.Count > 0)
                throw new ValidationException("CLI-002", errors);
        }

        private static string OnlyDigits(string? value)
            => new((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
