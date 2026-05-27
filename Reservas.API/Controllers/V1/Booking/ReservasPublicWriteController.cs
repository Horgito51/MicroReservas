using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reservas.API.Models.Requests.Public;
using Reservas.API.Models.Responses.Public;
using Reservas.Business.Exceptions;
using Reservas.Business.DTOs.Reservas;
using Reservas.Business.Interfaces.Reservas;
using Reservas.Business.Validators.Reservas;
using Reservas.API.Services;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Reservas.API.Controllers.V1.Booking
{
    [ApiController]
    [Authorize]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/public/reservas")]
    public class ReservasPublicWriteController : ControllerBase
    {
        private readonly IReservaService _reservaService;
        private readonly IClienteService _clienteService;
        private readonly IPublicReservaContractService _publicReservaService;

        public ReservasPublicWriteController(
            IReservaService reservaService,
            IClienteService clienteService,
            IPublicReservaContractService publicReservaService)
        {
            _reservaService = reservaService;
            _clienteService = clienteService;
            _publicReservaService = publicReservaService;
        }

        [HttpGet("{reservaGuid}")]
        [ProducesResponseType(typeof(ReservaPublicDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ReservaPublicDto>> GetByGuid(
            string reservaGuid)
        {
            var parsedReservaGuid = ParseRequiredGuid(reservaGuid, "reservaGuid");

            return Ok(await _publicReservaService.GetByGuidAsync(
                parsedReservaGuid,
                HttpContext.RequestAborted));
        }

        [HttpGet]
        public async Task<ActionResult> GetMisReservas(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            [FromQuery] string? estado = null)
        {
            var idCliente = await GetAuthenticatedClienteIdAsync();
            var filtro = new ReservaFiltroDTO 
            { 
                IdCliente = idCliente,
                EstadoReserva = estado ?? string.Empty
            };
            var result = await _reservaService.GetByFiltroAsync(filtro, page, limit);
            var items = new List<ReservaPublicDto>();
            foreach (var reserva in result.Items)
            {
                items.Add(await ToPublicReservaDtoAsync(reserva));
            }

            return Ok(new
            {
                items,
                result.TotalCount,
                result.PageNumber,
                result.PageSize
            });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<ReservaPublicDto>> Create([FromBody] PublicReservaCreateRequest request)
        {
            var response = await _publicReservaService.CreateAsync(request, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(GetByGuid), new { reservaGuid = response.ReservaGuid }, response);
        }

        private async Task<ClienteDTO> GetOrCreateClienteAsync(PublicClienteCreateRequest clienteRequest)
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
                    HttpContext.RequestAborted);
            }
            catch (NotFoundException)
            {
                try
                {
                    return await _clienteService.GetByCorreoAsync(clienteRequest.Correo, HttpContext.RequestAborted);
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
                    }, HttpContext.RequestAborted);
                }
            }
        }

        [HttpPatch("{reservaGuid:guid}/cancelar")]
        [AllowAnonymous]
        public async Task<IActionResult> Cancelar(Guid reservaGuid, [FromBody] PublicCancelarReservaRequest request)
        {
            if (reservaGuid == Guid.Empty)
                throw new ValidationException("RES-PUB-CAN-001", "reservaGuid es obligatorio.");

            request.ValidateNoIds();
            var reserva = await _reservaService.GetByGuidAsync(reservaGuid);
            await _reservaService.CancelarAsync(
                reserva.IdReserva,
                string.IsNullOrWhiteSpace(request.Motivo) ? "Cancelada desde flujo publico." : request.Motivo,
                User.Identity?.Name ?? "CLIENTE_PUBLICO",
                HttpContext.RequestAborted);

            return NoContent();
        }

        [HttpPost("calcular-precio")]
        [AllowAnonymous]
        public async Task<ActionResult<ReservaPrecioPublicDto>> CalcularPrecio([FromBody] PublicReservaPrecioRequest request)
        {
            request.ValidateNoIds();

            if (request.HabitacionGuid == Guid.Empty)
                throw new ValidationException("RES-PRECIO-PUB-001", "habitacionGuid es obligatorio.");

            if (request.FechaFin <= request.FechaInicio)
                throw new ValidationException("RES-PRECIO-PUB-002", "La fecha de fin debe ser posterior a la fecha de inicio.");

            var precio = await _publicReservaService.CalcularPrecioAsync(
                request.HabitacionGuid,
                request.FechaInicio,
                request.FechaFin,
                request.Canal,
                HttpContext.RequestAborted);

            return Ok(new ReservaPrecioPublicDto
            {
                HabitacionGuid = precio.HabitacionGuid,
                TarifaGuid = precio.TarifaGuid,
                PrecioNocheAplicado = precio.PrecioNocheAplicado,
                SubtotalLinea = precio.SubtotalLinea,
                ValorIvaLinea = precio.ValorIvaLinea,
                TotalLinea = precio.TotalLinea,
                OrigenPrecio = precio.OrigenPrecio
            });
        }

        private async Task<ReservaPublicDto> ToPublicReservaDtoAsync(ReservaDTO reserva)
        {
            var cliente = await _clienteService.GetByIdAsync(reserva.IdCliente);
            var habitaciones = new List<ReservaHabitacionPublicDto>();

            foreach (var detalle in reserva.Habitaciones ?? new List<ReservaHabitacionDTO>())
            {
                habitaciones.Add(new ReservaHabitacionPublicDto
                {
                    ReservaHabitacionGuid = detalle.ReservaHabitacionGuid,
                    HabitacionGuid = Guid.Empty,
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
                });
            }

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
                Habitaciones = habitaciones
            };
        }

        private async Task<int> GetAuthenticatedClienteIdAsync()
        {
            var idClienteClaim = User.Claims.FirstOrDefault(c => c.Type == "idCliente")?.Value;
            if (int.TryParse(idClienteClaim, out var idCliente) && idCliente > 0)
                return idCliente;

            var idUsuarioClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idUsuarioClaim, out var idUsuario))
                throw new UnauthorizedBusinessException("AUTH-CLIENTE-001", "Token sin identificacion de usuario.");

            throw new UnauthorizedBusinessException("AUTH-CLIENTE-002", "Token sin idCliente. La asociacion de usuarios pertenece al microservicio Seguridad.");
        }

        private (int? IdCliente, Guid? ClienteGuid, bool EsBackOffice) GetPublicReservationAuthContext()
        {
            int? idCliente = null;
            var idClienteClaim = User.Claims.FirstOrDefault(c => c.Type == "idCliente")?.Value;
            if (int.TryParse(idClienteClaim, out var parsedIdCliente) && parsedIdCliente > 0)
                idCliente = parsedIdCliente;

            Guid? clienteGuid = null;
            var clienteGuidClaim = User.Claims.FirstOrDefault(c =>
                c.Type.Equals("clienteGuid", StringComparison.OrdinalIgnoreCase) ||
                c.Type.Equals("ClienteGuid", StringComparison.OrdinalIgnoreCase))?.Value;
            if (Guid.TryParse(clienteGuidClaim, out var parsedClienteGuid) && parsedClienteGuid != Guid.Empty)
                clienteGuid = parsedClienteGuid;

            var esBackOffice = User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Any(role => role.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase) ||
                             role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                             role.Equals("RECEPCIONISTA", StringComparison.OrdinalIgnoreCase) ||
                             role.Equals("OPERATIVO", StringComparison.OrdinalIgnoreCase) ||
                             role.Equals("DESK_SERVICE", StringComparison.OrdinalIgnoreCase));

            return (idCliente, clienteGuid, esBackOffice);
        }

        private static Guid ParseRequiredGuid(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var guid) || guid == Guid.Empty)
                throw new ValidationException($"RES-PUB-{fieldName.ToUpperInvariant()}-001", $"{fieldName} es obligatorio y debe tener formato UUID valido.");

            return guid;
        }
    }
}
