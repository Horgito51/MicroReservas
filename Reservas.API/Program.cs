using System.Text;
using System.Security.Claims;
using System.Text.Json;
using Asp.Versioning;
using Alojamiento.Contracts.Grpc.V1;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Reservas.Business.Interfaces.Reservas;
using Reservas.Business.Services.Reservas;
using Reservas.API.Services;
using Reservas.API.GrpcServices;
using Reservas.DataAccess.Context;
using Reservas.DataAccess.Repositories.Interfaces.Reservas;
using Reservas.DataAccess.Repositories.Reservas;
using Reservas.DataManagement.Reservas.Interfaces;
using Reservas.DataManagement.Reservas.Services;
using Reservas.DataManagement.UnitOfWork;

const string LocalCorsPolicy = "LocalCorsPolicy";

string[] backOfficeRoles =
[
    "ADMINISTRADOR",
    "ADMIN",
    "RECEPCIONISTA",
    "OPERATIVO",
    "DESK_SERVICE"
];

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
ConfigureHttpEndpoint(builder);
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddHealthChecks();
var disableAuthorizationForTesting = builder.Configuration.GetValue<bool>("Security:DisableAuthorizationForTesting");
var connectionString = ResolveDefaultConnectionString(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Microservicio Reservas API", Version = "v1" });
    if (!disableAuthorizationForTesting)
    {
        AddBearerSecurity(options);
        options.OperationFilter<AuthorizeOperationFilter>();
    }
});
builder.Services.AddCors(options => options.AddPolicy(LocalCorsPolicy, policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddDbContext<ReservasDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions => sqlOptions.CommandTimeout(0)));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IReservaRepository, ReservaRepository>();
builder.Services.AddScoped<IReservaHabitacionRepository, ReservaHabitacionRepository>();
builder.Services.AddScoped<IClienteDataService, ClienteDataService>();
builder.Services.AddScoped<IReservaDataService, ReservaDataService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IReservaService, ReservaService>();
builder.Services.AddScoped<IPublicReservaContractService, PublicReservaContractService>();
builder.Services.AddHttpClient("AlojamientoHealth", client =>
{
    var baseUrl = builder.Configuration["Services:AlojamientoBaseUrl"]
        ?? throw new InvalidOperationException("La configuracion 'Services:AlojamientoBaseUrl' es obligatoria.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<IAlojamientoCatalogClient>(sp =>
{
    var baseUrl = builder.Configuration["Services:AlojamientoGrpcUrl"]
        ?? builder.Configuration["Services:AlojamientoBaseUrl"]
        ?? throw new InvalidOperationException("La configuracion 'Services:AlojamientoGrpcUrl' es obligatoria para crear reservas publicas.");
    var channel = CreateAlojamientoGrpcChannel(baseUrl);
    return new GrpcAlojamientoCatalogClient(new AlojamientoGrpc.AlojamientoGrpcClient(channel));
});
AddJwtAuthentication(builder.Services, builder.Configuration, builder.Environment, backOfficeRoles);
if (disableAuthorizationForTesting)
{
    builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TestingAuthorizationMiddlewareResultHandler>();
}

var app = builder.Build();
app.Logger.LogInformation("Iniciando {Service} en ambiente {Environment}", "Microservicio.Reservas", app.Environment.EnvironmentName);
app.Logger.LogInformation("Swagger habilitado en /swagger y /swagger/v1/swagger.json");
app.Logger.LogInformation("Cadena de conexion DefaultConnection cargada para {Service}", "Microservicio.Reservas");
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Microservicio Reservas API v1"));
app.UseMiddleware<Reservas.API.Middleware.ExceptionHandlingMiddleware>();
if (app.Configuration.GetValue("HttpsRedirection:Enabled", !app.Environment.IsDevelopment()))
{
    app.UseHttpsRedirection();
}
app.UseCors(LocalCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<AdminProfileAccessMiddleware>();
app.UseAuthorization();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Microservicio.Reservas" }));
app.MapGet("/health/version", () => Results.Ok(new
{
    service = "Microservicio.Reservas",
    build = "reservas-sucursal-guid-fix-20260520-01",
    sdk = "Microsoft.NET.Sdk.Web"
}));
app.MapGet("/health/db", async (ReservasDbContext dbContext, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("HealthDb");
    try
    {
        logger.LogInformation("Validando conexion a base de datos de Reservas");
        var canConnect = await dbContext.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { status = "ok", database = "connected", service = "Microservicio.Reservas" })
            : Results.Problem("No fue posible conectar a la base de datos de Reservas.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error validando conexion a base de datos de Reservas");
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/health/alojamiento", async (IHttpClientFactory httpClientFactory, IAlojamientoCatalogClient alojamientoClient, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("HealthAlojamiento");
    var result = new Dictionary<string, object?>();

    try
    {
        var httpClient = httpClientFactory.CreateClient("AlojamientoHealth");
        using var response = await httpClient.GetAsync("/health");
        result["httpStatusCode"] = (int)response.StatusCode;
        result["http"] = response.IsSuccessStatusCode ? "ok" : "unhealthy";
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Error validando /health de Alojamiento desde Reservas");
        result["http"] = "error";
        result["httpError"] = ex.Message;
    }

    try
    {
        var habitaciones = await alojamientoClient.GetHabitacionesAsync();
        result["catalog"] = "ok";
        result["habitacionesCount"] = habitaciones.Count;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Error validando cliente de catalogo de Alojamiento desde Reservas");
        result["catalog"] = "error";
        result["catalogError"] = ex.Message;
    }

    var healthy = string.Equals(result.GetValueOrDefault("http")?.ToString(), "ok", StringComparison.OrdinalIgnoreCase)
        && string.Equals(result.GetValueOrDefault("catalog")?.ToString(), "ok", StringComparison.OrdinalIgnoreCase);

    return healthy
        ? Results.Ok(new { status = "ok", service = "Microservicio.Reservas", alojamiento = result })
        : Results.Json(new { status = "degraded", service = "Microservicio.Reservas", alojamiento = result }, statusCode: StatusCodes.Status503ServiceUnavailable);
});
app.MapControllers();
app.MapGrpcService<ReservaGrpcService>();
app.MapGrpcService<ClienteGrpcService>();
app.Logger.LogInformation("{Service} listo para recibir solicitudes", "Microservicio.Reservas");
app.Run();

static void ConfigureHttpEndpoint(WebApplicationBuilder builder)
{
    var configuredPort = builder.Configuration.GetValue<int?>("Ports:Http");
    if (configuredPort is null)
        return;

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(configuredPort.Value, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        });
    });
}

static GrpcChannel CreateAlojamientoGrpcChannel(string baseUrl)
{
    var uri = new Uri(baseUrl);
    if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return GrpcChannel.ForAddress(uri, new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler())
        });
    }

    return GrpcChannel.ForAddress(uri);
}

static string ResolveDefaultConnectionString(IConfiguration configuration, IWebHostEnvironment environment)
{
    var configuredConnectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(configuredConnectionString) && HasPassword(configuredConnectionString))
        return configuredConnectionString;

    foreach (var basePath in new[]
    {
        environment.ContentRootPath,
        AppContext.BaseDirectory,
        Directory.GetCurrentDirectory()
    }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var fileConfiguration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .Build();

        var fileConnectionString = fileConfiguration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fileConnectionString) && HasPassword(fileConnectionString))
            return fileConnectionString;
    }

    if (string.IsNullOrWhiteSpace(configuredConnectionString))
        throw new InvalidOperationException("La cadena de conexion 'DefaultConnection' es obligatoria.");

    throw new InvalidOperationException(
        "La cadena de conexion 'DefaultConnection' esta incompleta: falta Password/Pwd. Revise la configuracion del App Service o appsettings.json.");
}

static bool HasPassword(string connectionString)
{
    try
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return !string.IsNullOrWhiteSpace(builder.Password);
    }
    catch (ArgumentException)
    {
        return connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Pwd=", StringComparison.OrdinalIgnoreCase);
    }
}

static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment, string[] backOfficeRoles)
{
    var secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("La configuracion 'Jwt:Secret' es obligatoria.");
    var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("La configuracion 'Jwt:Issuer' es obligatoria.");
    var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("La configuracion 'Jwt:Audience' es obligatoria.");

    if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        throw new InvalidOperationException("La configuracion 'Jwt:Secret' debe tener al menos 32 caracteres.");

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer");
                logger.LogWarning("JWT auth failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();

                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    return context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = false,
                        message = "No autorizado. Se requiere token de autenticacion valido.",
                        statusCode = StatusCodes.Status401Unauthorized,
                        errors = (object?)null,
                        traceId = context.HttpContext.TraceIdentifier,
                        timestamp = DateTime.UtcNow
                    }));
                }

                return Task.CompletedTask;
            }
        };
    });

    services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminProfile", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => !context.User.IsInRole("CLIENTE"));
        });

        options.AddPolicy("BackOffice", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(backOfficeRoles);
        });
    });
}
static void AddBearerSecurity(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT con el formato: Bearer {token}"
    });
}

sealed class AuthorizeOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        var hasAllowAnonymous = descriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()
            || descriptor.MethodInfo.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any()
            || descriptor.ControllerTypeInfo.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any();

        if (hasAllowAnonymous)
            return;

        var hasAuthorize = descriptor.EndpointMetadata.OfType<IAuthorizeData>().Any()
            || descriptor.MethodInfo.GetCustomAttributes(true).OfType<IAuthorizeData>().Any()
            || descriptor.ControllerTypeInfo.GetCustomAttributes(true).OfType<IAuthorizeData>().Any();

        if (!hasAuthorize)
            return;

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
        });
    }
}

sealed class TestingAuthorizationMiddlewareResultHandler(IConfiguration configuration) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (configuration.GetValue<bool>("Security:DisableAuthorizationForTesting"))
            return next(context);

        return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
sealed class AdminProfileAccessMiddleware(RequestDelegate next)
{
    private static readonly string[] BackOfficeRoles =
    [
        "ADMINISTRADOR",
        "ADMIN",
        "RECEPCIONISTA",
        "OPERATIVO",
        "DESK_SERVICE"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!RequiresAdminProfile(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "No autorizado. Se requiere autenticacion.");
            return;
        }

        var roles = context.User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToList();

        var isCliente = roles.Any(role => string.Equals(role, "CLIENTE", StringComparison.OrdinalIgnoreCase));
        var hasBackOfficeRole = roles.Any(role =>
            BackOfficeRoles.Any(allowed => string.Equals(role, allowed, StringComparison.OrdinalIgnoreCase)));

        if (!hasBackOfficeRole)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "Acceso denegado. Se requiere rol administrativo o de recepcion.");
            return;
        }

        if (isCliente)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "Acceso denegado. El rol CLIENTE no puede ingresar al perfil administrativo.");
            return;
        }

        await next(context);
    }

    private static bool RequiresAdminProfile(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Contains("/internal/", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("/internal/auth/", StringComparison.OrdinalIgnoreCase);
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
            return Task.CompletedTask;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            success = false,
            message,
            statusCode,
            errors = (object?)null,
            traceId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow
        }));
    }
}
