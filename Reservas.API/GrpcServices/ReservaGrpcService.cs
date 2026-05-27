using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Reservas.API.Models.Requests.Public;
using Reservas.API.Services;
using Reservas.Business.DTOs.Reservas;
using Reservas.Business.Interfaces.Reservas;
using Reservas.Contracts.Grpc.V1;

namespace Reservas.API.GrpcServices;

public class ReservaGrpcService : Reservas.Contracts.Grpc.V1.ReservaGrpc.ReservaGrpcBase
{
    private readonly IReservaService _reservaService;
    private readonly IClienteService _clienteService;
    private readonly IPublicReservaContractService _reservaContractService;
    private readonly IAlojamientoCatalogClient _alojamientoCatalogClient;

    public ReservaGrpcService(
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

    public override async Task<Reserva> GetReservaById(IdRequest request, ServerCallContext context)
    {
        try
        {
            return ToGrpc(await _reservaService.GetByIdAsync(request.Id, context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Reserva> GetReservaByGuid(GuidRequest request, ServerCallContext context)
    {
        try
        {
            return ToGrpc(await _reservaService.GetByGuidAsync(Guid.Parse(request.Guid), context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Reserva> CreateReserva(ReservaCreateRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new ReservaCreateDTO
            {
                IdCliente = request.IdCliente,
                IdSucursal = request.IdSucursal,
                SucursalGuid = string.IsNullOrWhiteSpace(request.SucursalGuid)
                    ? null
                    : Guid.Parse(request.SucursalGuid),
                FechaInicio = request.FechaInicio.ToDateTime(),
                FechaFin = request.FechaFin.ToDateTime(),
                SubtotalReserva = ParseDecimal(request.SubtotalReserva),
                ValorIva = ParseDecimal(request.ValorIva),
                TotalReserva = ParseDecimal(request.TotalReserva),
                DescuentoAplicado = ParseDecimal(request.DescuentoAplicado),
                SaldoPendiente = ParseDecimal(request.SaldoPendiente),
                OrigenCanalReserva = request.OrigenCanalReserva,
                EstadoReserva = string.IsNullOrWhiteSpace(request.EstadoReserva) ? "PEN" : request.EstadoReserva,
                Observaciones = request.Observaciones,
                EsWalkin = request.EsWalkin,
                Habitaciones = request.Habitaciones.Select(ToHabitacionDto).ToList()
            };

            return ToGrpc(await _reservaService.CreateAsync(dto, context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<ReservaPage> ListReservas(ReservaFiltroRequest request, ServerCallContext context)
    {
        try
        {
            var pageNumber = request.Page?.PageNumber > 0 ? request.Page.PageNumber : 1;
            var pageSize = request.Page?.PageSize > 0 ? request.Page.PageSize : 100;
            var filtro = new ReservaFiltroDTO
            {
                IdCliente = request.IdCliente,
                IdSucursal = request.IdSucursal,
                EstadoReserva = request.EstadoReserva ?? string.Empty,
                CodigoReserva = request.CodigoReserva ?? string.Empty,
                EsWalkin = request.EsWalkin,
                EsEliminado = request.EsEliminado
            };

            var result = await _reservaService.GetByFiltroAsync(filtro, pageNumber, pageSize, context.CancellationToken);
            var response = new ReservaPage
            {
                TotalCount = result.TotalCount,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize
            };
            response.Items.AddRange(result.Items.Select(ToGrpc));
            return response;
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Reserva> CrearReservaPublica(ReservaPublicCreateRequest request, ServerCallContext context)
    {
        try
        {
            var reserva = await _reservaContractService.CreateAsync(new PublicReservaCreateRequest
            {
                SucursalGuid = ParseGuid(request.SucursalGuid, "sucursal_guid"),
                FechaInicio = request.FechaInicio.ToDateTime(),
                FechaFin = request.FechaFin.ToDateTime(),
                OrigenCanalReserva = request.OrigenCanalReserva,
                Observaciones = request.Observaciones,
                Cliente = new PublicClienteCreateRequest
                {
                    TipoIdentificacion = request.Cliente?.TipoIdentificacion ?? string.Empty,
                    NumeroIdentificacion = request.Cliente?.NumeroIdentificacion ?? string.Empty,
                    Nombres = request.Cliente?.Nombres ?? string.Empty,
                    Apellidos = request.Cliente?.Apellidos,
                    Correo = request.Cliente?.Correo ?? string.Empty,
                    Telefono = request.Cliente?.Telefono ?? string.Empty,
                    Direccion = request.Cliente?.Direccion
                },
                Habitaciones = request.Habitaciones.Select(ToPublicHabitacionRequest).ToList()
            }, context.CancellationToken);

            return ToGrpc(await _reservaService.GetByGuidAsync(reserva.ReservaGuid, context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override Task<Reserva> CrearReservaAccommodation(ReservaPublicCreateRequest request, ServerCallContext context)
        => CrearReservaPublica(request, context);

    public override async Task<Reserva> CrearReservaInterna(ReservaInternalCreateRequest request, ServerCallContext context)
    {
        try
        {
            var cliente = await _clienteService.GetByGuidAsync(ParseGuid(request.ClienteGuid, "cliente_guid"), context.CancellationToken);
            var sucursal = await _alojamientoCatalogClient.GetSucursalAsync(ParseGuid(request.SucursalGuid, "sucursal_guid"), context.CancellationToken);
            var dto = new ReservaPorTipoHabitacionCreateDTO
            {
                IdCliente = cliente.IdCliente,
                IdSucursal = sucursal.IdSucursal,
                SucursalGuid = sucursal.SucursalGuid == Guid.Empty
                    ? ParseGuid(request.SucursalGuid, "sucursal_guid")
                    : sucursal.SucursalGuid,
                FechaInicio = request.FechaInicio.ToDateTime(),
                FechaFin = request.FechaFin.ToDateTime(),
                DescuentoAplicado = ParseDecimal(request.DescuentoAplicado),
                OrigenCanalReserva = string.IsNullOrWhiteSpace(request.OrigenCanalReserva) ? "INTERNO" : request.OrigenCanalReserva,
                Observaciones = request.Observaciones,
                EsWalkin = request.EsWalkin,
                ExigirPermiteReservaPublica = false,
                Habitaciones = request.Habitaciones.Select(ToTipoHabitacionDto).ToList()
            };

            return ToGrpc(await _reservaContractService.CreateInternalAsync(dto, context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<ReservaPrecio> CalcularPrecioHabitacion(CalcularPrecioHabitacionRequest request, ServerCallContext context)
    {
        try
        {
            var dto = await _reservaContractService.CalcularPrecioAsync(
                request.IdHabitacion,
                request.FechaInicio.ToDateTime(),
                request.FechaFin.ToDateTime(),
                request.Canal,
                context.CancellationToken);

            return new ReservaPrecio
            {
                IdHabitacion = dto.IdHabitacion,
                HabitacionGuid = dto.HabitacionGuid.ToString(),
                IdSucursal = dto.IdSucursal,
                IdTarifa = dto.IdTarifa,
                PrecioNocheAplicado = dto.PrecioNocheAplicado.ToString("0.##"),
                SubtotalLinea = dto.SubtotalLinea.ToString("0.##"),
                ValorIvaLinea = dto.ValorIvaLinea.ToString("0.##"),
                TotalLinea = dto.TotalLinea.ToString("0.##"),
                OrigenPrecio = dto.OrigenPrecio
            };
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Empty> CancelarReserva(CancelarReservaRequest request, ServerCallContext context)
    {
        try
        {
            await _reservaService.CancelarAsync(request.IdReserva, request.Motivo, request.Usuario, context.CancellationToken);
            return new Empty();
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    private static Reserva ToGrpc(ReservaDTO dto)
    {
        var grpc = new Reserva
        {
            IdReserva = dto.IdReserva,
            ReservaGuid = dto.GuidReserva.ToString(),
            CodigoReserva = dto.CodigoReserva ?? string.Empty,
            IdCliente = dto.IdCliente,
            IdSucursal = dto.IdSucursal,
            SucursalGuid = dto.SucursalGuid?.ToString() ?? string.Empty,
            FechaReservaUtc = Timestamp.FromDateTime(dto.FechaReservaUtc.ToUniversalTime()),
            FechaInicio = Timestamp.FromDateTime(dto.FechaInicio.ToUniversalTime()),
            FechaFin = Timestamp.FromDateTime(dto.FechaFin.ToUniversalTime()),
            SubtotalReserva = dto.SubtotalReserva.ToString("0.##"),
            ValorIva = dto.ValorIva.ToString("0.##"),
            TotalReserva = dto.TotalReserva.ToString("0.##"),
            DescuentoAplicado = dto.DescuentoAplicado.ToString("0.##"),
            SaldoPendiente = dto.SaldoPendiente.ToString("0.##"),
            OrigenCanalReserva = dto.OrigenCanalReserva ?? string.Empty,
            EstadoReserva = dto.EstadoReserva ?? string.Empty,
            MotivoCancelacion = dto.MotivoCancelacion ?? string.Empty,
            Observaciones = dto.Observaciones ?? string.Empty,
            EsWalkin = dto.EsWalkin,
            RowVersion = Google.Protobuf.ByteString.CopyFrom(dto.RowVersion ?? Array.Empty<byte>())
        };

        grpc.Habitaciones.AddRange((dto.Habitaciones ?? []).Select(ToGrpc));
        return grpc;
    }

    private static Reservas.Business.DTOs.Reservas.ReservaHabitacionDTO ToHabitacionDto(ReservaHabitacionCreateRequest request)
    {
        return new Reservas.Business.DTOs.Reservas.ReservaHabitacionDTO
        {
            IdHabitacion = request.IdHabitacion,
            FechaInicio = request.FechaInicio.ToDateTime(),
            FechaFin = request.FechaFin.ToDateTime(),
            NumAdultos = request.NumAdultos,
            NumNinos = request.NumNinos,
            PrecioNocheAplicado = ParseDecimal(request.PrecioNocheAplicado),
            SubtotalLinea = ParseDecimal(request.SubtotalLinea),
            ValorIvaLinea = ParseDecimal(request.ValorIvaLinea),
            DescuentoLinea = ParseDecimal(request.DescuentoLinea),
            TotalLinea = ParseDecimal(request.TotalLinea),
            EstadoDetalle = request.EstadoDetalle
        };
    }

    private static ReservaHabitacion ToGrpc(Reservas.Business.DTOs.Reservas.ReservaHabitacionDTO dto)
    {
        return new ReservaHabitacion
        {
            IdReservaHabitacion = dto.IdReservaHabitacion,
            ReservaHabitacionGuid = dto.ReservaHabitacionGuid.ToString(),
            IdReserva = dto.IdReserva,
            IdHabitacion = dto.IdHabitacion,
            HabitacionGuid = dto.HabitacionGuid?.ToString() ?? string.Empty,
            IdTarifa = dto.IdTarifa,
            TarifaGuid = dto.TarifaGuid?.ToString() ?? string.Empty,
            FechaInicio = Timestamp.FromDateTime(dto.FechaInicio.ToUniversalTime()),
            FechaFin = Timestamp.FromDateTime(dto.FechaFin.ToUniversalTime()),
            NumAdultos = dto.NumAdultos,
            NumNinos = dto.NumNinos,
            PrecioNocheAplicado = dto.PrecioNocheAplicado.ToString("0.##"),
            SubtotalLinea = dto.SubtotalLinea.ToString("0.##"),
            ValorIvaLinea = dto.ValorIvaLinea.ToString("0.##"),
            DescuentoLinea = dto.DescuentoLinea.ToString("0.##"),
            TotalLinea = dto.TotalLinea.ToString("0.##"),
            EstadoDetalle = dto.EstadoDetalle ?? string.Empty
        };
    }

    private static decimal ParseDecimal(string? value)
    {
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;

        return 0m;
    }

    private static PublicReservaHabitacionCreateRequest ToPublicHabitacionRequest(ReservaHabitacionTipoRequest request)
        => new()
        {
            TipoHabitacionGuid = ParseGuid(request.TipoHabitacionGuid, "tipo_habitacion_guid"),
            NumHabitaciones = request.NumHabitaciones,
            NumAdultos = request.NumAdultos,
            NumNinos = request.NumNinos
        };

    private static ReservaTipoHabitacionCreateDTO ToTipoHabitacionDto(ReservaHabitacionTipoRequest request)
        => new()
        {
            TipoHabitacionGuid = ParseGuid(request.TipoHabitacionGuid, "tipo_habitacion_guid"),
            NumHabitaciones = request.NumHabitaciones,
            NumAdultos = request.NumAdultos,
            NumNinos = request.NumNinos,
            DescuentoLinea = ParseDecimal(request.DescuentoLinea)
        };

    private static Guid ParseGuid(string value, string fieldName)
    {
        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} es obligatorio y debe ser un GUID valido."));

        return guid;
    }
}
