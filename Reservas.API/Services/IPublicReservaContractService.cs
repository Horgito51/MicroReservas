using Reservas.API.Models.Requests.Public;
using Reservas.API.Models.Responses.Public;
using Reservas.Business.DTOs.Reservas;

namespace Reservas.API.Services
{
    public interface IPublicReservaContractService
    {
        Task<ReservaPublicDto> CreateAsync(PublicReservaCreateRequest request, CancellationToken ct = default);
        Task<ReservaDTO> CreateInternalAsync(ReservaPorTipoHabitacionCreateDTO request, CancellationToken ct = default);
        Task<ReservaPrecioDTO> CalcularPrecioAsync(int idHabitacion, DateTime fechaInicio, DateTime fechaFin, string? canal = null, CancellationToken ct = default);
        Task<ReservaPrecioDTO> CalcularPrecioAsync(Guid habitacionGuid, DateTime fechaInicio, DateTime fechaFin, string? canal = null, CancellationToken ct = default);
        Task<ReservaPublicDto> GetByGuidAsync(
            Guid reservaGuid,
            CancellationToken ct = default);
    }
}
