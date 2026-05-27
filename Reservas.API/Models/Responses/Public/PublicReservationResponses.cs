using System;
using System.Collections.Generic;
using Reservas.Business.DTOs.Reservas;

namespace Reservas.API.Models.Responses.Public
{
    public class ClientePublicDto
    {
        public Guid ClienteGuid { get; set; }
        public string TipoIdentificacion { get; set; } = string.Empty;
        public string NumeroIdentificacion { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string Estado { get; set; } = "ACT";
    }

    public class ReservaPublicDto
    {
        public Guid ReservaGuid { get; set; }
        public string CodigoReserva { get; set; } = string.Empty;
        public Guid ClienteGuid { get; set; }
        public Guid SucursalGuid { get; set; }
        public DateTime FechaReservaUtc { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal SubtotalReserva { get; set; }
        public decimal ValorIva { get; set; }
        public decimal TotalReserva { get; set; }
        public decimal DescuentoAplicado { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string OrigenCanalReserva { get; set; } = string.Empty;
        public string EstadoReserva { get; set; } = string.Empty;
        public DateTime? FechaConfirmacionUtc { get; set; }
        public string? Observaciones { get; set; }
        public bool EsWalkin { get; set; }
        public List<ReservaHabitacionPublicDto> Habitaciones { get; set; } = new();
    }

    public class ReservaHabitacionPublicDto
    {
        public Guid ReservaHabitacionGuid { get; set; }
        public Guid HabitacionGuid { get; set; }
        public Guid? TarifaGuid { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int NumAdultos { get; set; }
        public int NumNinos { get; set; }
        public decimal PrecioNocheAplicado { get; set; }
        public decimal SubtotalLinea { get; set; }
        public decimal ValorIvaLinea { get; set; }
        public decimal DescuentoLinea { get; set; }
        public decimal TotalLinea { get; set; }
        public string EstadoDetalle { get; set; } = string.Empty;
    }

    public class ReservaPrecioPublicDto
    {
        public Guid HabitacionGuid { get; set; }
        public Guid? TarifaGuid { get; set; }
        public decimal PrecioNocheAplicado { get; set; }
        public decimal SubtotalLinea { get; set; }
        public decimal ValorIvaLinea { get; set; }
        public decimal TotalLinea { get; set; }
        public string OrigenPrecio { get; set; } = string.Empty;
    }

    public static class PublicReservationMapper
    {
        public static ClientePublicDto ToPublicDto(this ClienteDTO cliente)
            => new()
            {
                ClienteGuid = cliente.ClienteGuid,
                TipoIdentificacion = cliente.TipoIdentificacion,
                NumeroIdentificacion = cliente.NumeroIdentificacion,
                Nombres = cliente.Nombres,
                Apellidos = cliente.Apellidos,
                Correo = cliente.Correo,
                Telefono = cliente.Telefono,
                Direccion = cliente.Direccion,
                Estado = cliente.Estado
            };
    }
}
