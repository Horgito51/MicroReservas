using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservas.API.Models.Requests.Internal;
using Reservas.API.Models.Responses.Public;
using Reservas.Business.Interfaces.Reservas;
using System.Threading.Tasks;

namespace Reservas.API.Controllers.V1.Booking
{
    [ApiController]
    [AllowAnonymous]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/public/clientes")]
    public class ClientesPublicWriteController : ControllerBase
    {
        private readonly IClienteService _clienteService;

        public ClientesPublicWriteController(IClienteService clienteService)
        {
            _clienteService = clienteService;
        }

        [HttpGet("by-email")]
        public async Task<ActionResult<ClientePublicDto>> GetByEmail([FromQuery] string correo)
        {
            var result = await _clienteService.GetByCorreoAsync(correo);
            return Ok(result.ToPublicDto());
        }

        [HttpGet("{clienteGuid:guid}")]
        public async Task<ActionResult<ClientePublicDto>> GetByGuid(Guid clienteGuid)
        {
            var result = await _clienteService.GetByGuidAsync(clienteGuid);
            return Ok(result.ToPublicDto());
        }

        [HttpPost]
        public async Task<ActionResult<ClientePublicDto>> Create([FromBody] ClienteCreateRequest request)
        {
            var result = await _clienteService.CreateAsync(request.ToCreateDto());
            return CreatedAtAction(nameof(GetByGuid), new { clienteGuid = result.ClienteGuid }, result.ToPublicDto());
        }
    }
}
