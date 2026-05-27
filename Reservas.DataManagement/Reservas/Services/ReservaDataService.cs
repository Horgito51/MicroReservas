using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Reservas.DataAccess.Context;
using Reservas.DataAccess.Repositories.Interfaces.Reservas;
using Reservas.DataManagement.Exceptions;
using Reservas.DataManagement.Reservas.Interfaces;
using Reservas.DataManagement.Reservas.Models;
using Reservas.DataManagement.Reservas.Mappers;
using Reservas.DataManagement.Common;
using Reservas.DataManagement.UnitOfWork;

namespace Reservas.DataManagement.Reservas.Services
{
    public class ReservaDataService : IReservaDataService
    {
        private readonly IReservaRepository _reservaRepository;
        private readonly IReservaHabitacionRepository _reservaHabitacionRepository;
        private readonly IClienteRepository _clienteRepository;
        private readonly ReservasDbContext _context;
        private readonly IUnitOfWork _unitOfWork;

        public ReservaDataService(
            IReservaRepository reservaRepository,
            IReservaHabitacionRepository reservaHabitacionRepository,
            IClienteRepository clienteRepository,
            ReservasDbContext context,
            IUnitOfWork unitOfWork)
        {
            _reservaRepository = reservaRepository;
            _reservaHabitacionRepository = reservaHabitacionRepository;
            _clienteRepository = clienteRepository;
            _context = context;
            _unitOfWork = unitOfWork;
        }

        public async Task<ReservaDataModel> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _reservaRepository.GetByIdAsync(id, ct);
            return entity?.ToModel();
        }

        public async Task<ReservaDataModel> GetByGuidAsync(Guid guid, CancellationToken ct = default)
        {
            var entity = await _reservaRepository.GetByGuidAsync(guid, ct);
            return entity?.ToModel();
        }

        public async Task<ReservaDataModel> GetByCodigoAsync(string codigo, CancellationToken ct = default)
        {
            var entity = await _reservaRepository.GetByCodigoAsync(codigo, ct);
            return entity?.ToModel();
        }

        public async Task<DataPagedResult<ReservaDataModel>> GetByFiltroAsync(ReservaFiltroDataModel filtro, int pageNumber, int pageSize, CancellationToken ct = default)
        {
            var all = await _reservaRepository.GetAllAsync(ct);
            var query = all.AsQueryable();

            if (filtro.IdCliente.HasValue)
                query = query.Where(r => r.IdCliente == filtro.IdCliente.Value);
            if (filtro.IdSucursal.HasValue)
                query = query.Where(r => r.IdSucursal == filtro.IdSucursal.Value);
            if (!string.IsNullOrEmpty(filtro.EstadoReserva))
                query = query.Where(r => r.EstadoReserva == filtro.EstadoReserva);
            if (filtro.FechaInicioDesde.HasValue)
                query = query.Where(r => r.FechaInicio >= filtro.FechaInicioDesde.Value);
            if (filtro.FechaInicioHasta.HasValue)
                query = query.Where(r => r.FechaInicio <= filtro.FechaInicioHasta.Value);
            if (!string.IsNullOrEmpty(filtro.CodigoReserva))
                query = query.Where(r => r.CodigoReserva.Contains(filtro.CodigoReserva));
            if (filtro.EsWalkin.HasValue)
                query = query.Where(r => r.EsWalkin == filtro.EsWalkin.Value);
            if (filtro.EsEliminado.HasValue)
                query = query.Where(r => r.EsEliminado == filtro.EsEliminado.Value);

            var totalCount = query.Count();
            var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return new DataPagedResult<ReservaDataModel>
            {
                Items = items.ToModelList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<ReservaDataModel> AddAsync(ReservaDataModel model, CancellationToken ct = default)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (!model.SucursalGuid.HasValue || model.SucursalGuid.Value == Guid.Empty)
                throw new DomainException("sucursal_guid es obligatorio para crear la cabecera de reserva.");

            var requestedRooms = model.Habitaciones?.ToList() ?? new();
            if (requestedRooms.Count == 0)
                throw new DomainException("La reserva debe incluir al menos una habitacion.");

            var cliente = await _clienteRepository.GetByIdAsync(model.IdCliente, ct);
            if (cliente == null)
                throw new DomainException($"El cliente con ID {model.IdCliente} no existe.");

            int createdReservaId = 0;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // 1) Insertar cabecera sin detalles
                var entity = model.ToEntity();
                entity.SucursalGuid = model.SucursalGuid.Value;
                entity.ReservasHabitaciones = new System.Collections.Generic.List<global::Reservas.DataAccess.Entities.Reservas.ReservaHabitacionEntity>();

                if (entity.GuidReserva == Guid.Empty) entity.GuidReserva = Guid.NewGuid();
                if (string.IsNullOrWhiteSpace(entity.CreadoPorUsuario)) entity.CreadoPorUsuario = "Sistema";
                if (string.IsNullOrWhiteSpace(entity.ServicioOrigen)) entity.ServicioOrigen = "reservas-service";
                if (string.IsNullOrWhiteSpace(entity.CodigoReserva))
                    entity.CodigoReserva = $"RES-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
                entity.FechaInicio = entity.FechaInicio.Date;
                entity.FechaFin = entity.FechaFin.Date;
                entity.FechaRegistroUtc = DateTime.UtcNow;
                entity.FechaReservaUtc = DateTime.UtcNow;
                if (string.Equals(entity.EstadoReserva, "CON", StringComparison.OrdinalIgnoreCase) &&
                    !entity.FechaConfirmacionUtc.HasValue)
                    entity.FechaConfirmacionUtc = DateTime.UtcNow;

                var addedHeader = await _reservaRepository.AddAsync(entity, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                createdReservaId = addedHeader.IdReserva;
                // 3) Recalcular totales de cabecera en base a lo insertado por SP
                //    (SP_CONFIRMAR_RESERVA_HABITACION calcula montos por línea, pero no actualiza booking.RESERVAS)
                var totales = await _context.ReservasHabitaciones
                    .Where(rh => rh.IdReserva == createdReservaId)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Subtotal = g.Sum(x => x.SubtotalLinea),
                        Iva = g.Sum(x => x.ValorIvaLinea),
                        Total = g.Sum(x => x.TotalLinea)
                    })
                    .FirstOrDefaultAsync(ct)
                    ?? new { Subtotal = 0m, Iva = 0m, Total = 0m };

                var descuento = addedHeader.DescuentoAplicado;
                if (descuento < 0) descuento = 0;

                addedHeader.SubtotalReserva = totales.Subtotal;
                addedHeader.ValorIva = totales.Iva;
                addedHeader.TotalReserva = Math.Max(0, totales.Total - descuento);
                addedHeader.SaldoPendiente = addedHeader.TotalReserva;
                addedHeader.SucursalGuid = model.SucursalGuid.Value;
                addedHeader.ModificadoPorUsuario = string.IsNullOrWhiteSpace(model.CreadoPorUsuario) ? "Sistema" : model.CreadoPorUsuario;
                addedHeader.FechaModificacionUtc = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync(ct);
                createdReservaId = addedHeader.IdReserva;

                // 2) Insertar detalles vía SP_CONFIRMAR_RESERVA_HABITACION (calcula montos y precio_noche_aplicado)
                foreach (var room in requestedRooms)
                {
                    if (room.IdHabitacion <= 0)
                        throw new DomainException("Cada habitacion debe incluir un IdHabitacion valido.");

                    var idTarifa = room.IdTarifa;
                    var precioNoche = room.PrecioNocheAplicado;
                    if (precioNoche <= 0)
                        throw new DomainException("Cada habitacion debe incluir el precio noche aplicado validado por Alojamiento.");

                    var numAdultos = room.NumAdultos > 0 ? room.NumAdultos : 1;
                    var numNinos = room.NumNinos >= 0 ? room.NumNinos : 0;

                    await _reservaRepository.ConfirmarReservaHabitacionAsync(
                        createdReservaId,
                        room.IdHabitacion,
                        idTarifa,
                        model.FechaInicio,
                        model.FechaFin,
                        numAdultos,
                        numNinos,
                        precioNoche,
                        string.IsNullOrWhiteSpace(model.CreadoPorUsuario) ? "Sistema" : model.CreadoPorUsuario,
                        ct);

                    var createdDetail = await _context.ReservasHabitaciones
                        .Where(rh =>
                            rh.IdReserva == createdReservaId &&
                            rh.IdHabitacion == room.IdHabitacion &&
                            rh.FechaInicio == model.FechaInicio &&
                            rh.FechaFin == model.FechaFin)
                        .OrderByDescending(rh => rh.IdReservaHabitacion)
                        .FirstOrDefaultAsync(ct);

                    if (createdDetail != null)
                    {
                        createdDetail.HabitacionGuid = room.HabitacionGuid;
                        createdDetail.TarifaGuid = room.TarifaGuid;
                        if (!string.IsNullOrWhiteSpace(room.EstadoDetalle))
                            createdDetail.EstadoDetalle = room.EstadoDetalle;
                    }
                }

                await _unitOfWork.SaveChangesAsync(ct);

                // Limpiar tracking para poder reconsultar lo insertado vía SP
                var totalesFinales = await _context.ReservasHabitaciones
                    .Where(rh => rh.IdReserva == createdReservaId)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Subtotal = g.Sum(x => x.SubtotalLinea),
                        Iva = g.Sum(x => x.ValorIvaLinea),
                        Total = g.Sum(x => x.TotalLinea)
                    })
                    .FirstOrDefaultAsync(ct);

                if (totalesFinales == null)
                    throw new InvalidOperationException("No se generaron detalles de reserva; no se pueden calcular totales.");

                addedHeader.SubtotalReserva = totalesFinales.Subtotal;
                addedHeader.ValorIva = totalesFinales.Iva;
                addedHeader.TotalReserva = Math.Max(0, totalesFinales.Total - descuento);
                addedHeader.SaldoPendiente = addedHeader.TotalReserva;
                addedHeader.SucursalGuid = model.SucursalGuid.Value;
                addedHeader.ModificadoPorUsuario = string.IsNullOrWhiteSpace(model.CreadoPorUsuario) ? "Sistema" : model.CreadoPorUsuario;
                addedHeader.FechaModificacionUtc = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync(ct);

                _context.ChangeTracker.Clear();
            }, ct);

            var refreshed = await _reservaRepository.GetByIdAsync(createdReservaId, ct);
            if (refreshed == null)
                throw new InvalidOperationException("No se pudo recargar la reserva creada.");

            if (!refreshed.SucursalGuid.HasValue || refreshed.SucursalGuid.Value == Guid.Empty)
            {
                refreshed.SucursalGuid = model.SucursalGuid.Value;
                await _reservaRepository.UpdateAsync(refreshed, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return refreshed.ToModel();
        }

        public async Task UpdateAsync(ReservaDataModel model, CancellationToken ct = default)
        {
            var entity = await _reservaRepository.GetByIdAsync(model.IdReserva, ct);
            if (entity == null) return;

            // Actualizamos solo los campos que pueden cambiar
            entity.FechaInicio = model.FechaInicio;
            entity.FechaFin = model.FechaFin;
            entity.SubtotalReserva = model.SubtotalReserva;
            entity.ValorIva = model.ValorIva;
            entity.TotalReserva = model.TotalReserva;
            entity.DescuentoAplicado = model.DescuentoAplicado;
            entity.SaldoPendiente = model.SaldoPendiente;
            entity.EstadoReserva = model.EstadoReserva;
            entity.Observaciones = model.Observaciones;
            entity.ModificadoPorUsuario = model.ModificadoPorUsuario ?? "Sistema";
            entity.FechaModificacionUtc = DateTime.UtcNow;

            if (entity.ReservasHabitaciones != null)
            {
                foreach (var detalle in entity.ReservasHabitaciones)
                {
                    var noches = (int)(model.FechaFin.Date - model.FechaInicio.Date).TotalDays;
                    if (noches <= 0) noches = 1;

                    detalle.FechaInicio = model.FechaInicio;
                    detalle.FechaFin = model.FechaFin;
                    detalle.SubtotalLinea = Math.Round(detalle.PrecioNocheAplicado * noches, 2);
                    detalle.ValorIvaLinea = Math.Round(detalle.SubtotalLinea * 0.12m, 2);
                    detalle.TotalLinea = Math.Max(0, detalle.SubtotalLinea + detalle.ValorIvaLinea - detalle.DescuentoLinea);
                    detalle.EstadoDetalle = model.EstadoReserva;
                    detalle.ModificadoPorUsuario = model.ModificadoPorUsuario ?? "Sistema";
                    detalle.FechaModificacionUtc = DateTime.UtcNow;
                }
            }

            await _reservaRepository.UpdateAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            await _reservaRepository.DeleteAsync(id, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task ConfirmarAsync(int idReserva, string usuario, CancellationToken ct = default)
        {
            await _reservaRepository.ConfirmarAsync(idReserva, usuario, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task CancelarAsync(int idReserva, string motivo, string usuario, CancellationToken ct = default)
        {
            await _reservaRepository.CancelarAsync(idReserva, motivo, usuario, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task FinalizarAsync(int idReserva, string usuario, CancellationToken ct = default)
        {
            await _reservaRepository.FinalizarAsync(idReserva, usuario, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<bool> PuedeCancelarAsync(int idReserva, CancellationToken ct = default)
        {
            return await _reservaRepository.PuedeCancelarAsync(idReserva, ct);
        }

        public async Task<int> ConfirmarReservaHabitacionAsync(int idReserva, int idHabitacion, int? idTarifa, DateTime fechaInicio, DateTime fechaFin, int numAdultos, int numNinos, decimal precioNoche, string usuario, CancellationToken ct = default)
        {
            var result = await _reservaRepository.ConfirmarReservaHabitacionAsync(idReserva, idHabitacion, idTarifa, fechaInicio, fechaFin, numAdultos, numNinos, precioNoche, usuario, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return result;
        }

        public async Task<bool> ExisteSolapamientoAsync(int idHabitacion, DateTime fechaInicio, DateTime fechaFin, int? excludeIdReserva = null, CancellationToken ct = default)
        {
            return await _reservaHabitacionRepository.ExistsSolapamientoAsync(idHabitacion, fechaInicio, fechaFin, excludeIdReserva, ct);
        }
    }
}
