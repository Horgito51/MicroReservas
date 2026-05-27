using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Reservas.API.Models.Requests.Internal;
using Reservas.Business.DTOs.Reservas;
using Reservas.Business.Exceptions;
using Reservas.Business.Interfaces.Reservas;
using Reservas.Business.Validators.Reservas;
using Reservas.API.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Reservas.API.Controllers.V1.Internal.Reservas
{
    [ApiController]
    [Authorize(Roles = "ADMINISTRADOR,ADMIN,RECEPCIONISTA,OPERATIVO,DESK_SERVICE")]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/internal/reservas")]
    public class ReservaController : ControllerBase
    {
        private readonly IReservaService _reservaService;
        private readonly IClienteService _clienteService;
        private readonly IPublicReservaContractService _reservaContractService;
        private readonly IAlojamientoCatalogClient _alojamientoCatalogClient;

        public ReservaController(
            IReservaService reservaService,
            IClienteService clienteService,
            IPublicReservaContractService reservaContractService,
            IAlojamientoCatalogClient alojamientoCatalogClient)
        {
            _reservaService = reservaService;
            _clienteService = clienteService;
            _reservaContractService = reservaContractService;
            _alojamientoCatalogClient = alojamientoCatalogClient;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReservaDTO>>> GetAll()
        {
            var pagedResult = await _reservaService.GetByFiltroAsync(new ReservaFiltroDTO(), 1, 50);
            return Ok(pagedResult.Items);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ReservaDTO>> GetById(int id)
        {
            var result = await _reservaService.GetByIdAsync(id);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ReservaDTO>> Create([FromBody] InternalReservaCreateRequest request)
        {
            ValidateCreateRequest(request);
            var cliente = await ResolveClienteAsync(request);
            var sucursal = await _alojamientoCatalogClient.GetSucursalAsync(request.SucursalGuid, HttpContext.RequestAborted);
            var dto = request.ToCreateDto(cliente.IdCliente, sucursal.IdSucursal);
            var result = await _reservaContractService.CreateInternalAsync(dto, HttpContext.RequestAborted);

            return CreatedAtAction(nameof(GetById), new { id = result.IdReserva }, result);
        }

        private async Task<ClienteDTO> ResolveClienteAsync(InternalReservaCreateRequest request)
        {
            if (request.ClienteGuid != Guid.Empty)
                return await _clienteService.GetByGuidAsync(request.ClienteGuid, HttpContext.RequestAborted);

            return await GetOrCreateClienteAsync(request.Cliente!, HttpContext.RequestAborted);
        }

        private async Task<ClienteDTO> GetOrCreateClienteAsync(ClienteCreateRequest clienteRequest, CancellationToken ct)
        {
            ClienteValidator.Validate(new ClienteDTO
            {
                TipoIdentificacion = clienteRequest.TipoIdentificacion,
                NumeroIdentificacion = clienteRequest.NumeroIdentificacion,
                Nombres = clienteRequest.Nombres,
                Apellidos = clienteRequest.Apellidos ?? string.Empty,
                RazonSocial = clienteRequest.RazonSocial ?? string.Empty,
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
                    return await _clienteService.CreateAsync(clienteRequest.ToCreateDto(), ct);
                }
            }
        }

        private static void ValidateCreateRequest(InternalReservaCreateRequest request)
        {
            if (request == null)
                throw new ValidationException("RES-INT-001", "El cuerpo de la reserva es obligatorio.");
            if (request.ClienteGuid == Guid.Empty && request.Cliente == null)
                throw new ValidationException("RES-INT-CLI-001", "clienteGuid o cliente es obligatorio.");
            if (request.ClienteGuid == Guid.Empty && request.Cliente != null &&
                (string.IsNullOrWhiteSpace(request.Cliente.TipoIdentificacion) ||
                 string.IsNullOrWhiteSpace(request.Cliente.NumeroIdentificacion) ||
                 string.IsNullOrWhiteSpace(request.Cliente.Nombres) ||
                 string.IsNullOrWhiteSpace(request.Cliente.Correo) ||
                 string.IsNullOrWhiteSpace(request.Cliente.Telefono)))
                throw new ValidationException("RES-INT-CLI-002", "tipoIdentificacion, numeroIdentificacion, nombres, correo y telefono son obligatorios.");
            if (request.SucursalGuid == Guid.Empty)
                throw new ValidationException("RES-INT-SUC-001", "sucursalGuid es obligatorio.");
            if (request.FechaInicio == default)
                throw new ValidationException("RES-INT-002", "fechaInicio es obligatoria.");
            if (request.FechaFin == default || request.FechaFin <= request.FechaInicio)
                throw new ValidationException("RES-INT-003", "fechaFin debe ser posterior a fechaInicio.");
            if (request.Habitaciones == null || request.Habitaciones.Count == 0)
                throw new ValidationException("RES-INT-004", "La reserva debe incluir al menos un tipo de habitacion.");

            foreach (var habitacion in request.Habitaciones)
            {
                if (habitacion.TipoHabitacionGuid == Guid.Empty)
                    throw new ValidationException("RES-INT-005", "tipoHabitacionGuid es obligatorio.");
                if (habitacion.NumHabitaciones <= 0)
                    throw new ValidationException("RES-INT-006", "numHabitaciones debe ser mayor a cero.");
                if (habitacion.NumAdultos <= 0)
                    throw new ValidationException("RES-INT-007", "numAdultos debe ser mayor a cero.");
                if (habitacion.NumNinos < 0)
                    throw new ValidationException("RES-INT-008", "numNinos no puede ser negativo.");
            }
        }

        [HttpPost("calcular-precio")]
        public async Task<ActionResult<ReservaPrecioDTO>> CalcularPrecio([FromBody] ReservaPrecioRequest request)
        {
            var result = await _reservaContractService.CalcularPrecioAsync(
                request.IdHabitacion,
                request.FechaInicio,
                request.FechaFin,
                request.Canal,
                HttpContext.RequestAborted);

            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ReservaUpdateRequest request)
        {
            var dto = request.ToUpdateDto(id);
            await _reservaService.UpdateAsync(dto);
            return NoContent();
        }

        [HttpPatch("{id}/confirmar")]
        public async Task<IActionResult> Confirm(int id)
        {
            await _reservaService.ConfirmarAsync(id, "Sistema");
            return NoContent();
        }

        [HttpPatch("{id}/cancelar")]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelarReservaRequest request)
        {
            await _reservaService.CancelarAsync(id, request.Motivo, "Sistema");
            return NoContent();
        }
    }
}
