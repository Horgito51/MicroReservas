using Reservas.API.Models.Requests.Public;
using Reservas.API.Models.Responses.Public;
using Reservas.Business.DTOs.Reservas;
using Reservas.Business.Exceptions;
using Reservas.Business.Interfaces.Reservas;
using Reservas.Business.Validators.Reservas;

namespace Reservas.API.Services
{
    public sealed class PublicReservaContractService : IPublicReservaContractService
    {
        private readonly IReservaService _reservaService;
        private readonly IClienteService _clienteService;
        private readonly IAlojamientoCatalogClient _alojamientoClient;

        public PublicReservaContractService(
            IReservaService reservaService,
            IClienteService clienteService,
            IAlojamientoCatalogClient alojamientoClient)
        {
            _reservaService = reservaService;
            _clienteService = clienteService;
            _alojamientoClient = alojamientoClient;
        }

        public async Task<ReservaPublicDto> CreateAsync(PublicReservaCreateRequest request, CancellationToken ct = default)
        {
            ValidateCreateRequest(request);

            var cliente = await GetOrCreateClienteAsync(request.Cliente!, ct);
            var sucursal = await _alojamientoClient.GetSucursalAsync(request.SucursalGuid, ct);
            var sucursalGuid = sucursal.SucursalGuid == Guid.Empty ? request.SucursalGuid : sucursal.SucursalGuid;
            var reserva = await CreateInternalAsync(new ReservaPorTipoHabitacionCreateDTO
            {
                IdCliente = cliente.IdCliente,
                IdSucursal = sucursal.IdSucursal,
                SucursalGuid = sucursalGuid,
                FechaInicio = request.FechaInicio.Date,
                FechaFin = request.FechaFin.Date,
                DescuentoAplicado = 0m,
                OrigenCanalReserva = string.IsNullOrWhiteSpace(request.OrigenCanalReserva)
                    ? "MARKETPLACE"
                    : request.OrigenCanalReserva,
                Observaciones = request.Observaciones ?? string.Empty,
                EsWalkin = false,
                ExigirPermiteReservaPublica = true,
                Habitaciones = request.Habitaciones.Select(h => new ReservaTipoHabitacionCreateDTO
                {
                    TipoHabitacionGuid = h.TipoHabitacionGuid,
                    NumHabitaciones = h.NumHabitaciones,
                    NumAdultos = h.NumAdultos,
                    NumNinos = h.NumNinos,
                    DescuentoLinea = 0m
                }).ToList()
            }, ct);

            if (!reserva.SucursalGuid.HasValue || reserva.SucursalGuid.Value == Guid.Empty)
                reserva.SucursalGuid = sucursalGuid;

            return await ToPublicReservaDtoAsync(reserva, ct);
        }

        public async Task<ReservaDTO> CreateInternalAsync(ReservaPorTipoHabitacionCreateDTO request, CancellationToken ct = default)
        {
            ValidateReservaPorTipoHabitacion(request);
            var habitaciones = await ResolveHabitacionesAsync(request, ct);

            var reserva = await _reservaService.CreateAsync(new ReservaCreateDTO
            {
                IdCliente = request.IdCliente,
                IdSucursal = request.IdSucursal,
                SucursalGuid = request.SucursalGuid,
                FechaInicio = request.FechaInicio.Date,
                FechaFin = request.FechaFin.Date,
                DescuentoAplicado = request.DescuentoAplicado,
                OrigenCanalReserva = string.IsNullOrWhiteSpace(request.OrigenCanalReserva)
                    ? "MARKETPLACE"
                    : request.OrigenCanalReserva,
                EstadoReserva = "CON",
                Observaciones = request.Observaciones ?? string.Empty,
                EsWalkin = request.EsWalkin,
                Habitaciones = habitaciones
            }, ct);

            if (!reserva.SucursalGuid.HasValue || reserva.SucursalGuid.Value == Guid.Empty)
                reserva.SucursalGuid = request.SucursalGuid;

            await MarcarHabitacionesOcupadasAsync(reserva, ct);
            return reserva;
        }

        public async Task<ReservaPrecioDTO> CalcularPrecioAsync(int idHabitacion, DateTime fechaInicio, DateTime fechaFin, string? canal = null, CancellationToken ct = default)
        {
            if (idHabitacion <= 0)
                throw new ValidationException("RES-PRECIO-INT-001", "idHabitacion es obligatorio.");
            if (fechaFin <= fechaInicio)
                throw new ValidationException("RES-PRECIO-INT-002", "La fecha de fin debe ser posterior a la fecha de inicio.");

            var habitacion = await _alojamientoClient.GetHabitacionAsync(idHabitacion, ct);
            return await CalcularPrecioAsync(habitacion, fechaInicio.Date, fechaFin.Date, canal, ct);
        }

        public async Task<ReservaPrecioDTO> CalcularPrecioAsync(Guid habitacionGuid, DateTime fechaInicio, DateTime fechaFin, string? canal = null, CancellationToken ct = default)
        {
            if (habitacionGuid == Guid.Empty)
                throw new ValidationException("RES-PRECIO-PUB-001", "habitacionGuid es obligatorio.");
            if (fechaFin <= fechaInicio)
                throw new ValidationException("RES-PRECIO-PUB-002", "La fecha de fin debe ser posterior a la fecha de inicio.");

            var habitacion = await _alojamientoClient.GetHabitacionAsync(habitacionGuid, ct);
            return await CalcularPrecioAsync(habitacion, fechaInicio.Date, fechaFin.Date, canal, ct);
        }

        public async Task<ReservaPublicDto> GetByGuidAsync(
            Guid reservaGuid,
            CancellationToken ct = default)
        {
            if (reservaGuid == Guid.Empty)
                throw new ValidationException("RES-PUB-GET-001", "reservaGuid es obligatorio y debe tener formato UUID valido.");

            var reserva = await _reservaService.GetByGuidAsync(reservaGuid, ct);
            var cliente = await _clienteService.GetByIdAsync(reserva.IdCliente, ct);

            return ToPublicReservaDto(reserva, cliente);
        }

        private async Task<List<ReservaHabitacionDTO>> ResolveHabitacionesAsync(ReservaPorTipoHabitacionCreateDTO request, CancellationToken ct)
        {
            var result = new List<ReservaHabitacionDTO>();

            foreach (var item in request.Habitaciones)
            {
                var inicio = request.FechaInicio.Date;
                var fin = request.FechaFin.Date;

                if (fin <= inicio)
                    throw new ValidationException("RES-PUB-HAB-001", "La fecha de fin de la habitacion debe ser posterior a la fecha de inicio.");

                var cantidad = item.NumHabitaciones;
                var tipo = await _alojamientoClient.GetTipoHabitacionAsync(item.TipoHabitacionGuid, ct);
                if (!string.Equals(tipo.EstadoTipoHabitacion, "ACT", StringComparison.OrdinalIgnoreCase) ||
                    (request.ExigirPermiteReservaPublica && !tipo.PermiteReservaPublica))
                    throw new ValidationException("RES-PUB-TIPO-001", "El tipo de habitacion no permite reserva publica.");

                if (item.NumAdultos > tipo.CapacidadAdultos || item.NumNinos > tipo.CapacidadNinos ||
                    item.NumAdultos + item.NumNinos > tipo.CapacidadTotal)
                    throw new ValidationException("RES-PUB-TIPO-002", $"La capacidad solicitada excede el tipo de habitacion {tipo.NombreTipoHabitacion}.");

                var selected = (await _alojamientoClient.GetHabitacionesDisponiblesAsync(
                        request.IdSucursal,
                        inicio,
                        fin,
                        item.TipoHabitacionGuid,
                        ct))
                    .Where(h => h.IdTipoHabitacion == tipo.IdTipoHabitacion)
                    .Take(cantidad)
                    .ToList();

                if (selected.Count < cantidad)
                    throw new ConflictException("No hay habitaciones disponibles suficientes para la solicitud.");

                foreach (var habitacion in selected)
                {
                    var estadoDetalleInicial = "CON";
                    var tarifa = await _alojamientoClient.GetTarifaVigenteRangoAsync(
                        habitacion.IdSucursal,
                        habitacion.IdTipoHabitacion,
                        inicio,
                        fin,
                        request.OrigenCanalReserva,
                        ct);
                    var precioNocheAplicado = tarifa?.PrecioPorNoche ?? habitacion.PrecioBase;

                    result.Add(new ReservaHabitacionDTO
                    {
                        IdHabitacion = habitacion.IdHabitacion,
                        HabitacionGuid = habitacion.HabitacionGuid,
                        IdTarifa = tarifa?.IdTarifa,
                        TarifaGuid = tarifa?.TarifaGuid,
                        FechaInicio = inicio,
                        FechaFin = fin,
                        NumAdultos = item.NumAdultos,
                        NumNinos = item.NumNinos,
                        PrecioNocheAplicado = precioNocheAplicado,
                        DescuentoLinea = item.DescuentoLinea,
                        EstadoDetalle = estadoDetalleInicial
                    });
                }
            }

            return result;
        }

        private async Task<ReservaPrecioDTO> CalcularPrecioAsync(AlojamientoHabitacionRef habitacion, DateTime fechaInicio, DateTime fechaFin, string? canal, CancellationToken ct)
        {
            var noches = (int)(fechaFin.Date - fechaInicio.Date).TotalDays;
            if (noches <= 0) noches = 1;

            var tarifa = await _alojamientoClient.GetTarifaVigenteRangoAsync(
                habitacion.IdSucursal,
                habitacion.IdTipoHabitacion,
                fechaInicio,
                fechaFin,
                canal,
                ct);

            var precioNoche = tarifa?.PrecioPorNoche ?? habitacion.PrecioBase;
            var ivaRate = (tarifa?.PorcentajeIva ?? 12m) / 100m;
            var subtotal = Math.Round(precioNoche * noches, 2);
            var iva = Math.Round(subtotal * ivaRate, 2);

            return new ReservaPrecioDTO
            {
                IdHabitacion = habitacion.IdHabitacion,
                HabitacionGuid = habitacion.HabitacionGuid,
                IdSucursal = habitacion.IdSucursal,
                IdTarifa = tarifa?.IdTarifa,
                TarifaGuid = tarifa?.TarifaGuid,
                PrecioNocheAplicado = precioNoche,
                SubtotalLinea = subtotal,
                ValorIvaLinea = iva,
                TotalLinea = subtotal + iva,
                OrigenPrecio = tarifa is null ? "PRECIO_BASE" : "TARIFA"
            };
        }

        private async Task<ClienteDTO> GetOrCreateClienteAsync(PublicClienteCreateRequest clienteRequest, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(clienteRequest.TipoIdentificacion) ||
                string.IsNullOrWhiteSpace(clienteRequest.NumeroIdentificacion) ||
                string.IsNullOrWhiteSpace(clienteRequest.Nombres) ||
                string.IsNullOrWhiteSpace(clienteRequest.Correo) ||
                string.IsNullOrWhiteSpace(clienteRequest.Telefono))
            {
                throw new ValidationException("RES-PUB-CLI-001", "tipoIdentificacion, numeroIdentificacion, nombres, correo y telefono son obligatorios.");
            }

            ClienteValidator.Validate(new ClienteDTO
            {
                TipoIdentificacion = clienteRequest.TipoIdentificacion,
                NumeroIdentificacion = clienteRequest.NumeroIdentificacion,
                Nombres = clienteRequest.Nombres,
                Apellidos = clienteRequest.Apellidos ?? string.Empty,
                RazonSocial = string.Empty,
                Correo = clienteRequest.Correo,
                Telefono = clienteRequest.Telefono,
                Direccion = clienteRequest.Direccion ?? string.Empty,
                Estado = "ACT"
            });

            try
            {
                return await _clienteService.GetByIdentificacionAsync(
                    clienteRequest.TipoIdentificacion,
                    clienteRequest.NumeroIdentificacion,
                    ct);
            }
            catch (NotFoundException)
            {
                try
                {
                    return await _clienteService.GetByCorreoAsync(clienteRequest.Correo, ct);
                }
                catch (NotFoundException)
                {
                    return await _clienteService.CreateAsync(new ClienteCreateDTO
                    {
                        TipoIdentificacion = clienteRequest.TipoIdentificacion,
                        NumeroIdentificacion = clienteRequest.NumeroIdentificacion,
                        Nombres = clienteRequest.Nombres,
                        Apellidos = clienteRequest.Apellidos ?? string.Empty,
                        RazonSocial = string.Empty,
                        Correo = clienteRequest.Correo,
                        Telefono = clienteRequest.Telefono,
                        Direccion = clienteRequest.Direccion ?? string.Empty,
                        Estado = "ACT"
                    }, ct);
                }
            }
        }

        private async Task MarcarHabitacionesOcupadasAsync(ReservaDTO reserva, CancellationToken ct)
        {
            foreach (var habitacion in reserva.Habitaciones ?? new List<ReservaHabitacionDTO>())
            {
                await _alojamientoClient.SetHabitacionEstadoAsync(
                    habitacion.IdHabitacion,
                    "OCU",
                    "reservas-service",
                    ct);
            }
        }

        private async Task<ReservaPublicDto> ToPublicReservaDtoAsync(ReservaDTO reserva, CancellationToken ct)
        {
            var cliente = await _clienteService.GetByIdAsync(reserva.IdCliente, ct);

            return ToPublicReservaDto(reserva, cliente);
        }

        private static ReservaPublicDto ToPublicReservaDto(ReservaDTO reserva, ClienteDTO cliente)
        {
            return new ReservaPublicDto
            {
                ReservaGuid = reserva.GuidReserva,
                CodigoReserva = reserva.CodigoReserva,
                ClienteGuid = cliente.ClienteGuid,
                SucursalGuid = reserva.SucursalGuid ?? Guid.Empty,
                FechaReservaUtc = reserva.FechaReservaUtc,
                FechaInicio = reserva.FechaInicio,
                FechaFin = reserva.FechaFin,
                SubtotalReserva = reserva.SubtotalReserva,
                ValorIva = reserva.ValorIva,
                TotalReserva = reserva.TotalReserva,
                DescuentoAplicado = reserva.DescuentoAplicado,
                SaldoPendiente = reserva.SaldoPendiente,
                OrigenCanalReserva = reserva.OrigenCanalReserva,
                EstadoReserva = reserva.EstadoReserva,
                FechaConfirmacionUtc = reserva.FechaConfirmacionUtc,
                Observaciones = reserva.Observaciones,
                EsWalkin = reserva.EsWalkin,
                Habitaciones = (reserva.Habitaciones ?? new List<ReservaHabitacionDTO>())
                    .Select(detalle => new ReservaHabitacionPublicDto
                    {
                        ReservaHabitacionGuid = detalle.ReservaHabitacionGuid,
                        HabitacionGuid = detalle.HabitacionGuid ?? Guid.Empty,
                        TarifaGuid = detalle.TarifaGuid,
                        FechaInicio = detalle.FechaInicio,
                        FechaFin = detalle.FechaFin,
                        NumAdultos = detalle.NumAdultos,
                        NumNinos = detalle.NumNinos,
                        PrecioNocheAplicado = detalle.PrecioNocheAplicado,
                        SubtotalLinea = detalle.SubtotalLinea,
                        ValorIvaLinea = detalle.ValorIvaLinea,
                        DescuentoLinea = detalle.DescuentoLinea,
                        TotalLinea = detalle.TotalLinea,
                        EstadoDetalle = detalle.EstadoDetalle
                    })
                    .ToList()
            };
        }

        private static void ValidateCreateRequest(PublicReservaCreateRequest request)
        {
            if (request == null)
                throw new ValidationException("RES-PUB-REQ-001", "El cuerpo de la reserva es obligatorio.");

            request.ValidateNoIds();

            if (request.SucursalGuid == Guid.Empty)
                throw new ValidationException("RES-PUB-004", "sucursalGuid es obligatorio.");
            if (request.FechaInicio == default)
                throw new ValidationException("RES-PUB-005", "fechaInicio es obligatoria.");
            if (request.FechaFin == default || request.FechaFin <= request.FechaInicio)
                throw new ValidationException("RES-PUB-001", "La fecha de fin debe ser posterior a la fecha de inicio.");
            if (request.Cliente == null)
                throw new ValidationException("RES-PUB-003", "cliente es obligatorio.");
            if (request.Habitaciones == null || request.Habitaciones.Count == 0)
                throw new ValidationException("RES-PUB-002", "La reserva debe tener al menos una habitacion.");

            foreach (var habitacion in request.Habitaciones)
            {
                if (habitacion.TipoHabitacionGuid == Guid.Empty)
                    throw new ValidationException("RES-PUB-HAB-002", "tipoHabitacionGuid es obligatorio.");
                if (habitacion.NumHabitaciones <= 0 || habitacion.NumAdultos <= 0 || habitacion.NumNinos < 0)
                    throw new ValidationException("RES-PUB-HAB-003", "numHabitaciones y numAdultos deben ser positivos; numNinos no puede ser negativo.");
            }
        }

        private static void ValidateReservaPorTipoHabitacion(ReservaPorTipoHabitacionCreateDTO request)
        {
            if (request.IdCliente <= 0)
                throw new ValidationException("RES-TIPO-001", "IdCliente es obligatorio.");
            if (request.IdSucursal <= 0)
                throw new ValidationException("RES-TIPO-002", "IdSucursal es obligatorio.");
            if (!request.SucursalGuid.HasValue || request.SucursalGuid.Value == Guid.Empty)
                throw new ValidationException("RES-TIPO-SUC-001", "sucursalGuid es obligatorio.");
            if (request.FechaFin <= request.FechaInicio)
                throw new ValidationException("RES-TIPO-003", "La fecha de fin debe ser posterior a la fecha de inicio.");
            if (request.Habitaciones == null || request.Habitaciones.Count == 0)
                throw new ValidationException("RES-TIPO-004", "Toda reserva debe solicitar al menos un tipo de habitacion.");

            foreach (var item in request.Habitaciones)
            {
                if (item.TipoHabitacionGuid == Guid.Empty)
                    throw new ValidationException("RES-TIPO-008", "tipoHabitacionGuid es obligatorio.");
                if (item.NumHabitaciones <= 0)
                    throw new ValidationException("RES-TIPO-009", "numHabitaciones debe ser mayor a cero.");
                if (item.NumAdultos <= 0)
                    throw new ValidationException("RES-TIPO-010", "numAdultos debe ser mayor a cero.");
                if (item.NumNinos < 0)
                    throw new ValidationException("RES-TIPO-011", "numNinos no puede ser negativo.");
                if (item.DescuentoLinea < 0)
                    throw new ValidationException("RES-TIPO-013", "descuentoLinea no puede ser negativo.");
            }
        }
    }
}
