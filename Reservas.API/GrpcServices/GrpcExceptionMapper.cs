using Grpc.Core;
using Reservas.Business.Exceptions;

namespace Reservas.API.GrpcServices;

internal static class GrpcExceptionMapper
{
    public static RpcException Map(Exception exception)
    {
        return exception switch
        {
            NotFoundException ex => new RpcException(new Status(StatusCode.NotFound, ex.Message)),
            ConflictException ex => new RpcException(new Status(StatusCode.Aborted, ex.Message)),
            ValidationException ex => new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)),
            ForbiddenBusinessException ex => new RpcException(new Status(StatusCode.PermissionDenied, ex.Message)),
            _ => new RpcException(new Status(StatusCode.Internal, "Error interno en servicio gRPC de Reservas."))
        };
    }
}
