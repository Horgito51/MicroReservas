using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reservas.DataAccess.Entities.Reservas;

namespace Reservas.DataAccess.Repositories.Interfaces.Reservas
{
    public interface IReservaRepository
    {
        // CRUD b�sico
        Task<ReservaEntity?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ReservaEntity?> GetByGuidAsync(Guid guid, CancellationToken ct = default);
        Task<ReservaEntity?> GetByCodigoAsync(string codigo, CancellationToken ct = default);
        Task<IEnumerable<ReservaEntity>> GetAllAsync(CancellationToken ct = default);
        Task<ReservaEntity> AddAsync(ReservaEntity entity, CancellationToken ct = default);
        Task UpdateAsync(ReservaEntity entity, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);

        // Operaciones de cambio de estado
        Task ConfirmarAsync(int idReserva, string usuario, CancellationToken ct = default);
        Task CancelarAsync(int idReserva, string motivo, string usuario, CancellationToken ct = default);
        Task FinalizarAsync(int idReserva, string usuario, CancellationToken ct = default);

        // Validaciones
        Task<bool> PuedeCancelarAsync(int idReserva, CancellationToken ct = default);

        // M�todo para confirmar reserva de habitaci�n (ejecuta SP)
        Task<int> ConfirmarReservaHabitacionAsync(int idReserva, int idHabitacion, int? idTarifa, DateTime fechaInicio, DateTime fechaFin, int numAdultos, int numNinos, decimal precioNoche, string usuario, CancellationToken ct = default);
    }
}