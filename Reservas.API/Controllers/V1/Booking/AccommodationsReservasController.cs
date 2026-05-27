using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Reservas.API.Models.Requests.Public;
using Reservas.API.Models.Responses.Public;
using Reservas.API.Services;
using Reservas.Business.Exceptions;
using System.Security.Claims;

namespace Reservas.API.Controllers.V1.Booking
{
    [ApiController]
    [Authorize]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accommodations/reservas")]
    public sealed class AccommodationsReservasController : ControllerBase
    {
        private readonly IPublicReservaContractService _publicReservaService;

        public AccommodationsReservasController(IPublicReservaContractService publicReservaService)
        {
            _publicReservaService = publicReservaService;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<ReservaPublicDto>> Create([FromBody] PublicReservaCreateRequest request)
        {
            var response = await _publicReservaService.CreateAsync(request, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(GetByGuid), new { reservaGuid = response.ReservaGuid }, response);
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
                throw new ValidationException($"RES-ACC-{fieldName.ToUpperInvariant()}-001", $"{fieldName} es obligatorio y debe tener formato UUID valido.");

            return guid;
        }
    }
}
