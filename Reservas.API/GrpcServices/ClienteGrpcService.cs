using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Reservas.Business.DTOs.Reservas;
using Reservas.Business.Interfaces.Reservas;
using Reservas.Contracts.Grpc.V1;

namespace Reservas.API.GrpcServices;

public class ClienteGrpcService : Reservas.Contracts.Grpc.V1.ClienteGrpc.ClienteGrpcBase
{
    private readonly IClienteService _clienteService;

    public ClienteGrpcService(IClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    public override async Task<Cliente> GetClienteById(IdRequest request, ServerCallContext context)
    {
        try
        {
            return ToGrpc(await _clienteService.GetByIdAsync(request.Id, context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Cliente> GetClienteByGuid(GuidRequest request, ServerCallContext context)
    {
        try
        {
            return ToGrpc(await _clienteService.GetByGuidAsync(Guid.Parse(request.Guid), context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Cliente> GetClienteByCorreo(CorreoRequest request, ServerCallContext context)
    {
        try
        {
            return ToGrpc(await _clienteService.GetByCorreoAsync(request.Correo, context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Cliente> CreateCliente(ClienteCreateRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new ClienteCreateDTO
            {
                TipoIdentificacion = request.TipoIdentificacion,
                NumeroIdentificacion = request.NumeroIdentificacion,
                Nombres = request.Nombres,
                Apellidos = request.Apellidos,
                RazonSocial = request.RazonSocial,
                Correo = request.Correo,
                Telefono = request.Telefono,
                Direccion = request.Direccion,
                Estado = request.Estado
            };

            return ToGrpc(await _clienteService.CreateAsync(dto, context.CancellationToken));
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Empty> UpdateCliente(ClienteUpdateRequest request, ServerCallContext context)
    {
        try
        {
            var dto = new ClienteUpdateDTO
            {
                IdCliente = request.IdCliente,
                Nombres = request.Nombres,
                Apellidos = request.Apellidos,
                RazonSocial = request.RazonSocial,
                Correo = request.Correo,
                Telefono = request.Telefono,
                Direccion = request.Direccion,
                Estado = request.Estado
            };

            await _clienteService.UpdateAsync(dto, context.CancellationToken);
            return new Empty();
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    public override async Task<Empty> DeleteCliente(IdRequest request, ServerCallContext context)
    {
        try
        {
            await _clienteService.DeleteAsync(request.Id, context.CancellationToken);
            return new Empty();
        }
        catch (Exception ex)
        {
            throw GrpcExceptionMapper.Map(ex);
        }
    }

    private static Cliente ToGrpc(ClienteDTO dto)
    {
        return new Cliente
        {
            IdCliente = dto.IdCliente,
            ClienteGuid = dto.ClienteGuid.ToString(),
            TipoIdentificacion = dto.TipoIdentificacion ?? string.Empty,
            NumeroIdentificacion = dto.NumeroIdentificacion ?? string.Empty,
            Nombres = dto.Nombres ?? string.Empty,
            Apellidos = dto.Apellidos ?? string.Empty,
            RazonSocial = dto.RazonSocial ?? string.Empty,
            Correo = dto.Correo ?? string.Empty,
            Telefono = dto.Telefono ?? string.Empty,
            Direccion = dto.Direccion ?? string.Empty,
            Estado = dto.Estado ?? string.Empty,
            EsEliminado = dto.EsEliminado,
            RowVersion = Google.Protobuf.ByteString.CopyFrom(dto.RowVersion ?? Array.Empty<byte>())
        };
    }
}
