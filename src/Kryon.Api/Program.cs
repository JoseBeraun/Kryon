using Kryon.Api.Errores;
using Kryon.Api.Seguridad;
using Kryon.Core.Seguridad;
using Kryon.Core.Usuarios;
using Kryon.Infrastructure.Persistencia;
using Kryon.Infrastructure.Usuarios;

using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

const string PoliticaCorsKryonWebLocal = "KryonWebLocal";

// Cadena de conexión desde configuración: User Secrets en Development y Key Vault (u otra fuente segura) en Azure.
const string NombreCadenaConexion = "Kryon";

var builder = WebApplication.CreateBuilder(args);

var entornoDePruebas = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test");

// Opciones provisionales de la Gestión de Usuarios; se validan al arrancar para no arrancar con una configuración inutilizable.
builder.Services.AddOptions<OpcionesUsuarios>()
    .BindConfiguration(OpcionesUsuarios.Seccion)
    .Validate(o => o.Paginacion.TamanoPorDefecto > 0, "Usuarios:Paginacion:TamanoPorDefecto debe ser mayor que 0.")
    .Validate(o => o.Paginacion.TamanoMaximo > 0, "Usuarios:Paginacion:TamanoMaximo debe ser mayor que 0.")
    .Validate(
        o => o.Paginacion.TamanoPorDefecto <= o.Paginacion.TamanoMaximo,
        "Usuarios:Paginacion:TamanoPorDefecto no puede superar a TamanoMaximo.")
    .Validate(o => o.Validacion.LongitudMaximaNombre > 0, "Usuarios:Validacion:LongitudMaximaNombre debe ser mayor que 0.")
    .Validate(
        o => o.Validacion.LongitudMaximaIdentificador > 0,
        "Usuarios:Validacion:LongitudMaximaIdentificador debe ser mayor que 0.")
    .ValidateOnStart();

// Seguridad. La autenticación real la definirá su propia spec; IdentidadPrueba solo existe en Development y Test.
// Fuera de esos entornos no hay ningún esquema y los encabezados X-Kryon-Prueba-* no autentican a nadie.
if (entornoDePruebas)
{
    builder.Services.AddAuthentication(IdentidadPrueba.Esquema)
        .AddScheme<AuthenticationSchemeOptions, ManejadorIdentidadPrueba>(IdentidadPrueba.Esquema, configureOptions: null);
}
else
{
    // Sin autenticación real todavía: un esquema que no autentica a nadie, para responder 401 en vez de fallar.
    builder.Services.AddAuthentication(SinAutenticacion.Esquema)
        .AddScheme<AuthenticationSchemeOptions, ManejadorSinAutenticacion>(SinAutenticacion.Esquema, configureOptions: null);
}

builder.Services.AgregarPoliticasUsuarios();

// Contexto de la solicitud: solo desde los claims verificados. Si no se puede construir, falla cerrado.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IContextoSolicitud>(proveedor =>
{
    var usuario = proveedor.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
    return ContextoSolicitudDesdeClaims.TryCrear(usuario, out var contexto)
        ? contexto
        : throw new InvalidOperationException("La solicitud no tiene un contexto verificado de usuario y empresa.");
});

// Persistencia. Todo por solicitud: el interceptor y el contexto usan el IContextoSolicitud de esa misma solicitud.
// La API nunca aplica migraciones al arrancar (research §R12).
builder.Services.AddScoped<ContextoEmpresaInterceptor>();
builder.Services.AddDbContext<KryonDbContext>((proveedor, opciones) =>
{
    var cadenaConexion = proveedor.GetRequiredService<IConfiguration>().GetConnectionString(NombreCadenaConexion);
    opciones
        .UseSqlServer(cadenaConexion)
        .AddInterceptors(proveedor.GetRequiredService<ContextoEmpresaInterceptor>());
});

// Implementaciones de Infrastructure de las interfaces de Core (T016) que ya existen. Los puntos de integración de
// DEP-2 a DEP-5 no se registran.
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddScoped<IRegistroAuditoria, RegistroAuditoria>();
builder.Services.AddScoped<IConsultaUsuarios, ConsultaUsuarios>();

// Excepciones no controladas: 500 genérico del contrato en todos los entornos (FR-041, SC-004).
// UseExceptionHandler() sin parámetros exige un IProblemDetailsService; el manejador responde antes que él.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorExcepcionesInesperadas>();

// CORS solo para Development y Test: permite que Kryon.Web local envíe los encabezados de IdentidadPrueba.
// Fuera de esos entornos la política no existe.
if (entornoDePruebas)
{
    builder.Services.AddCors(opciones =>
    {
        opciones.AddPolicy(PoliticaCorsKryonWebLocal, politica =>
        {
            // Orígenes de src/Kryon.Web/Properties/launchSettings.json (perfiles https y http).
            politica.WithOrigins("https://localhost:7288", "http://localhost:5106");

            // Métodos que usa contracts/usuarios-api.openapi.yaml.
            politica.WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put);

            // Content-Type (application/json) e If-Match (concurrencia) no son encabezados simples de CORS.
            politica.WithHeaders(
                IdentidadPrueba.EncabezadoUsuarioId,
                IdentidadPrueba.EncabezadoEmpresaId,
                IdentidadPrueba.EncabezadoCapacidades,
                HeaderNames.ContentType,
                HeaderNames.IfMatch);
        });
    });
}

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString(NombreCadenaConexion)))
{
    throw new InvalidOperationException(
        $"Falta la cadena de conexión 'ConnectionStrings:{NombreCadenaConexion}'. "
        + "En Development se configura con User Secrets; en Azure, desde Key Vault.");
}

// Primero en el pipeline para capturar las excepciones de todo lo que viene después.
app.UseExceptionHandler();

// Justo dentro: atiende solo las excepciones con la respuesta ya iniciada, que UseExceptionHandler no puede manejar.
app.UseMiddleware<BarreraRespuestaIniciada>();

app.UseHttpsRedirection();

if (entornoDePruebas)
{
    app.UseCors(PoliticaCorsKryonWebLocal);
}

app.UseAuthentication();
app.UseAuthorization();

app.Run();
