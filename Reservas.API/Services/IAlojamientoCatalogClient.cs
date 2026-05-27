namespace Reservas.API.Services
{
    public interface IAlojamientoCatalogClient
    {
        Task<AlojamientoSucursalRef> GetSucursalAsync(Guid sucursalGuid, CancellationToken ct = default);
        Task<AlojamientoTipoHabitacionRef> GetTipoHabitacionAsync(Guid tipoHabitacionGuid, CancellationToken ct = default);
        Task<AlojamientoHabitacionRef> GetHabitacionAsync(int idHabitacion, CancellationToken ct = default);
        Task<AlojamientoHabitacionRef> GetHabitacionAsync(Guid habitacionGuid, CancellationToken ct = default);
        Task<IReadOnlyList<AlojamientoHabitacionRef>> GetHabitacionesAsync(CancellationToken ct = default);
        Task<IReadOnlyList<AlojamientoHabitacionRef>> GetHabitacionesDisponiblesAsync(
            int idSucursal,
            DateTime fechaInicio,
            DateTime fechaFin,
            Guid? tipoHabitacionGuid = null,
            CancellationToken ct = default);
        Task<AlojamientoTarifaRef?> GetTarifaVigenteRangoAsync(
            int idSucursal,
            int idTipoHabitacion,
            DateTime fechaInicio,
            DateTime fechaFin,
            string? canal = null,
            CancellationToken ct = default);
        Task SetHabitacionEstadoAsync(
            int idHabitacion,
            string nuevoEstado,
            string usuario,
            CancellationToken ct = default);
    }

    public sealed class AlojamientoSucursalRef
    {
        public int IdSucursal { get; set; }
        public Guid SucursalGuid { get; set; }
    }

    public sealed class AlojamientoTipoHabitacionRef
    {
        public int IdTipoHabitacion { get; set; }
        public Guid TipoHabitacionGuid { get; set; }
        public bool PermiteReservaPublica { get; set; }
        public string EstadoTipoHabitacion { get; set; } = string.Empty;
        public int CapacidadAdultos { get; set; }
        public int CapacidadNinos { get; set; }
        public int CapacidadTotal { get; set; }
        public string NombreTipoHabitacion { get; set; } = string.Empty;
    }

    public sealed class AlojamientoHabitacionRef
    {
        public int IdHabitacion { get; set; }
        public Guid HabitacionGuid { get; set; }
        public int IdSucursal { get; set; }
        public int IdTipoHabitacion { get; set; }
        public decimal PrecioBase { get; set; }
        public string EstadoHabitacion { get; set; } = string.Empty;
        public bool EsEliminado { get; set; }
    }

    public sealed class AlojamientoTarifaRef
    {
        public int IdTarifa { get; set; }
        public Guid TarifaGuid { get; set; }
        public int IdSucursal { get; set; }
        public int IdTipoHabitacion { get; set; }
        public decimal PrecioPorNoche { get; set; }
        public decimal PorcentajeIva { get; set; }
    }
}
