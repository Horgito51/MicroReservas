using Alojamiento.Contracts.Grpc.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Reservas.Business.Exceptions;

namespace Reservas.API.Services
{
    public sealed class GrpcAlojamientoCatalogClient : IAlojamientoCatalogClient
    {
        private readonly AlojamientoGrpc.AlojamientoGrpcClient _client;

        public GrpcAlojamientoCatalogClient(AlojamientoGrpc.AlojamientoGrpcClient client)
        {
            _client = client;
        }

        public async Task<AlojamientoSucursalRef> GetSucursalAsync(Guid sucursalGuid, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetSucursalByGuidAsync(
                    new GuidRequest { Guid = sucursalGuid.ToString() },
                    cancellationToken: ct);

                return new AlojamientoSucursalRef
                {
                    IdSucursal = response.IdSucursal,
                    SucursalGuid = ParseGuid(response.SucursalGuid)
                };
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                throw new NotFoundException("ALOJ-SUC-404", "No se encontro la sucursal solicitada.");
            }
            catch (RpcException ex)
            {
                throw new ValidationException("ALOJ-GRPC-001", $"Alojamiento respondio {ex.StatusCode}. {ex.Status.Detail}");
            }
        }

        public async Task<AlojamientoTipoHabitacionRef> GetTipoHabitacionAsync(Guid tipoHabitacionGuid, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetTipoHabitacionByGuidAsync(
                    new GuidRequest { Guid = tipoHabitacionGuid.ToString() },
                    cancellationToken: ct);

                return new AlojamientoTipoHabitacionRef
                {
                    IdTipoHabitacion = response.IdTipoHabitacion,
                    TipoHabitacionGuid = ParseGuid(response.TipoHabitacionGuid),
                    PermiteReservaPublica = response.PermiteReservaPublica,
                    EstadoTipoHabitacion = response.EstadoTipoHabitacion,
                    CapacidadAdultos = response.CapacidadAdultos,
                    CapacidadNinos = response.CapacidadNinos,
                    CapacidadTotal = response.CapacidadTotal,
                    NombreTipoHabitacion = response.NombreTipoHabitacion
                };
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                throw new NotFoundException("ALOJ-TIPO-404", "No se encontro el tipo de habitacion solicitado.");
            }
            catch (RpcException ex)
            {
                throw new ValidationException("ALOJ-GRPC-002", $"Alojamiento respondio {ex.StatusCode}. {ex.Status.Detail}");
            }
        }

        public async Task<AlojamientoHabitacionRef> GetHabitacionAsync(int idHabitacion, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetHabitacionByIdAsync(
                    new IdRequest { Id = idHabitacion },
                    cancellationToken: ct);

                return ToRef(response);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                throw new NotFoundException("ALOJ-HAB-404", "No se encontro la habitacion solicitada.");
            }
            catch (RpcException ex)
            {
                throw new ValidationException("ALOJ-GRPC-006", $"Alojamiento respondio {ex.StatusCode}. {ex.Status.Detail}");
            }
        }

        public async Task<AlojamientoHabitacionRef> GetHabitacionAsync(Guid habitacionGuid, CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetHabitacionByGuidAsync(
                    new GuidRequest { Guid = habitacionGuid.ToString() },
                    cancellationToken: ct);

                return ToRef(response);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                throw new NotFoundException("ALOJ-HAB-404", "No se encontro la habitacion solicitada.");
            }
            catch (RpcException ex)
            {
                throw new ValidationException("ALOJ-GRPC-007", $"Alojamiento respondio {ex.StatusCode}. {ex.Status.Detail}");
            }
        }

        public async Task<IReadOnlyList<AlojamientoHabitacionRef>> GetHabitacionesAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _client.ListHabitacionesAsync(new Empty(), cancellationToken: ct);
                return response.Items.Select(ToRef).ToList();
            }
            catch (RpcException ex)
            {
                throw new ValidationException("ALOJ-GRPC-003", $"Alojamiento respondio {ex.StatusCode}. {ex.Status.Detail}");
            }
        }

        public async Task<AlojamientoTarifaRef?> GetTarifaVigenteRangoAsync(
            int idSucursal,
            int idTipoHabitacion,
            DateTime fechaInicio,
            DateTime fechaFin,
            string? canal = null,
            CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetTarifaVigenteRangoAsync(
                    new GetTarifaVigenteRangoRequest
                    {
                        IdSucursal = idSucursal,
                        IdTipoHabitacion = idTipoHabitacion,
                        FechaInicio = Timestamp.FromDateTime(fechaInicio.ToUniversalTime()),
                        FechaFin = Timestamp.FromDateTime(fechaFin.ToUniversalTime()),
                        Canal = canal ?? string.Empty
                    },
                    cancellationToken: ct);

                return response.Encontrada ? ToRef(response.Tarifa) : null;
            }
            catch (RpcException ex)
            {
                throw new ValidationException("ALOJ-GRPC-008", $"Alojamiento respondio {ex.StatusCode}. {ex.Status.Detail}");
            }
        }

        public async Task<IReadOnlyList<AlojamientoHabitacionRef>> GetHabitacionesDisponiblesAsync(
            int idSucursal,
            DateTime fechaInicio,
            DateTime fechaFin,
            Guid? tipoHabitacionGuid = null,
            CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetHabitacionesDisponiblesAsync(
                    new HabitacionesDisponiblesRequest
                    {
                        IdSucursal = idSucursal,
                        FechaInicio = Timestamp.FromDateTime(fechaInicio.ToUniversalTime()),
                        FechaFin = Timestamp.FromDateTime(fechaFin.ToUniversalTime())
                    },
                    cancellationToken: ct);

                return response.Items.Select(ToRef).ToList();
            }
            catch (RpcException ex)
            {
                throw new ValidationException("ALOJ-GRPC-004", $"Alojamiento respondio {ex.StatusCode}. {ex.Status.Detail}");
            }
        }

        public async Task SetHabitacionEstadoAsync(
            int idHabitacion,
            string nuevoEstado,
            string usuario,
            CancellationToken ct = default)
        {
            try
            {
                await _client.SetHabitacionEstadoAsync(
                    new SetHabitacionEstadoRequest
                    {
                        IdHabitacion = idHabitacion,
                        NuevoEstado = nuevoEstado,
                        Usuario = string.IsNullOrWhiteSpace(usuario) ? "reservas-service" : usuario
                    },
                    cancellationToken: ct);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                throw new NotFoundException("ALOJ-HAB-404", $"No se encontro la habitacion {idHabitacion} en Alojamiento.");
            }
            catch (RpcException ex)
            {
                throw new ValidationException("ALOJ-GRPC-005", $"Alojamiento respondio {ex.StatusCode}. {ex.Status.Detail}");
            }
        }

        private static AlojamientoHabitacionRef ToRef(Habitacion habitacion)
            => new()
            {
                IdHabitacion = habitacion.IdHabitacion,
                HabitacionGuid = ParseGuid(habitacion.HabitacionGuid),
                IdSucursal = habitacion.IdSucursal,
                IdTipoHabitacion = habitacion.IdTipoHabitacion,
                PrecioBase = ParseDecimal(habitacion.PrecioBase),
                EstadoHabitacion = habitacion.EstadoHabitacion,
                EsEliminado = habitacion.EsEliminado
            };

        private static AlojamientoTarifaRef ToRef(Tarifa tarifa)
            => new()
            {
                IdTarifa = tarifa.IdTarifa,
                TarifaGuid = ParseGuid(tarifa.TarifaGuid),
                IdSucursal = tarifa.IdSucursal,
                IdTipoHabitacion = tarifa.IdTipoHabitacion,
                PrecioPorNoche = ParseDecimal(tarifa.PrecioPorNoche),
                PorcentajeIva = ParseDecimal(tarifa.PorcentajeIva)
            };

        private static Guid ParseGuid(string value)
            => Guid.TryParse(value, out var guid) ? guid : Guid.Empty;

        private static decimal ParseDecimal(string value)
            => decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : 0m;
    }
}
