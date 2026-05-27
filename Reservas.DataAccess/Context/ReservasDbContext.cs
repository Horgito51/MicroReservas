using Microsoft.EntityFrameworkCore;
using Reservas.DataAccess.Entities.Reservas;

namespace Reservas.DataAccess.Context
{
    public class ReservasDbContext : DbContext
    {
        public ReservasDbContext(DbContextOptions<ReservasDbContext> options) : base(options)
        {
        }

        public DbSet<ClienteEntity> Clientes => Set<ClienteEntity>();
        public DbSet<ReservaEntity> Reservas => Set<ReservaEntity>();
        public DbSet<ReservaHabitacionEntity> ReservasHabitaciones => Set<ReservaHabitacionEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReservasDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
