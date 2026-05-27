using System;
using System.Collections.Generic;
using System.Linq;
using Reservas.Business.DTOs.Reservas;

namespace Reservas.API.Models.Requests.Internal
{
    public class ClienteCreateRequest
    {
        public string TipoIdentificacion { get; set; } = string.Empty;
        public string NumeroIdentificacion { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string? RazonSocial { get; set; }
        public string Correo { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string Estado { get; set; } = "ACT";
    }

    public class ClienteUpdateRequest
    {
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string? RazonSocial { get; set; }
        public string Correo { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string Estado { get; set; } = "ACT";
    }

    public class InternalReservaCreateRequest
    {
        public Guid ClienteGuid { get; set; }
        public ClienteCreateRequest? Cliente { get; set; }
        public Guid SucursalGuid { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal DescuentoAplicado { get; set; }
        public string? Observaciones { get; set; }
        public bool EsWalkin { get; set; }
        public string? OrigenCanalReserva { get; set; } = "INTERNO";
        public List<InternalReservaHabitacionTipoRequest> Habitaciones { get; set; } = new();
    }

    public class InternalReservaHabitacionTipoRequest
    {
        public Guid TipoHabitacionGuid { get; set; }
        public int NumHabitaciones { get; set; } = 1;
        public int NumAdultos { get; set; } = 1;
        public int NumNinos { get; set; }
        public decimal DescuentoLinea { get; set; }
    }

    public class ReservaUpdateRequest
    {
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public decimal SubtotalReserva { get; set; }
        public decimal ValorIva { get; set; }
        public decimal TotalReserva { get; set; }
        public decimal DescuentoAplicado { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string EstadoReserva { get; set; } = "PEN";
        public string? Observaciones { get; set; }
    }

    public class CancelarReservaRequest
    {
        public string Motivo { get; set; } = string.Empty;
    }

    public class ReservaPrecioRequest
    {
        public int IdHabitacion { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string? Canal { get; set; }
    }

    public static class InternalRequestContractsMapper
    {
        public static ClienteCreateDTO ToCreateDto(this ClienteCreateRequest request)
            => new()
            {
                TipoIdentificacion = request.TipoIdentificacion,
                NumeroIdentificacion = request.NumeroIdentificacion,
                Nombres = request.Nombres,
                Apellidos = request.Apellidos,
                RazonSocial = request.RazonSocial,
                Correo = request.Correo,
                Telefono = request.Telefono,
                Direccion = request.Direccion,
                Estado = request.Estado
            };

        public static ClienteUpdateDTO ToUpdateDto(this ClienteUpdateRequest request, int id)
            => new()
            {
                IdCliente = id,
                Nombres = request.Nombres,
                Apellidos = request.Apellidos,
                RazonSocial = request.RazonSocial,
                Correo = request.Correo,
                Telefono = request.Telefono,
                Direccion = request.Direccion,
                Estado = request.Estado
            };

        public static ReservaPorTipoHabitacionCreateDTO ToCreateDto(this InternalReservaCreateRequest request, int idCliente, int idSucursal)
            => new()
            {
                IdCliente = idCliente,
                IdSucursal = idSucursal,
                SucursalGuid = request.SucursalGuid,
                FechaInicio = request.FechaInicio,
                FechaFin = request.FechaFin,
                DescuentoAplicado = request.DescuentoAplicado,
                OrigenCanalReserva = string.IsNullOrWhiteSpace(request.OrigenCanalReserva) ? "INTERNO" : request.OrigenCanalReserva,
                Observaciones = request.Observaciones ?? string.Empty,
                EsWalkin = request.EsWalkin,
                ExigirPermiteReservaPublica = false,
                Habitaciones = request.Habitaciones.Select(h => new ReservaTipoHabitacionCreateDTO
                {
                    TipoHabitacionGuid = h.TipoHabitacionGuid,
                    NumHabitaciones = h.NumHabitaciones,
                    NumAdultos = h.NumAdultos,
                    NumNinos = h.NumNinos,
                    DescuentoLinea = h.DescuentoLinea
                }).ToList()
            };

        public static ReservaUpdateDTO ToUpdateDto(this ReservaUpdateRequest request, int id)
            => new()
            {
                IdReserva = id,
                FechaInicio = request.FechaInicio,
                FechaFin = request.FechaFin,
                SubtotalReserva = request.SubtotalReserva,
                ValorIva = request.ValorIva,
                TotalReserva = request.TotalReserva,
                DescuentoAplicado = request.DescuentoAplicado,
                SaldoPendiente = request.SaldoPendiente,
                EstadoReserva = request.EstadoReserva,
                Observaciones = request.Observaciones ?? string.Empty
            };
    }
}
