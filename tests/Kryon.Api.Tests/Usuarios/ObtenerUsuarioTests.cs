using System.Data;
using System.Net;
using System.Text.Json;

using Kryon.Api.Tests.Infraestructura;
using Kryon.Contracts.Usuarios;
using Kryon.Core.Usuarios;

using Microsoft.Data.SqlClient;

namespace Kryon.Api.Tests.Usuarios;

/// <summary>
/// <c>GET /api/usuarios/{id}</c> (US3-1, US3-3, FR-003, FR-046) sobre la API real, SQL Server real y los datos semilla:
/// el detalle con sus acciones y su <c>ETag</c>, y el mismo 404 para un id de otra empresa y uno inexistente, auditado
/// igual en ambos casos.
/// </summary>
public sealed class ObtenerUsuarioTests(KryonApiFactory fabrica) : IClassFixture<KryonApiFactory>
{
    /// <summary>Id que no existe en ninguna empresa.</summary>
    private static readonly Guid IdInventado = Guid.Parse("f0000000-0000-0000-0000-0000000000a3");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UsuarioDeLaEmpresa_Responde200ConDatosAccionesYETag()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var beto = SemillaDatos.Beto;

        var respuesta = await ana.GetAsync($"/api/usuarios/{beto.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = JsonSerializer.Deserialize<DetalleUsuario>(await respuesta.Content.ReadAsStringAsync(ct), Json)!;
        Assert.Equal(beto.Id, detalle.Id);
        Assert.Equal(beto.NombreCompleto, detalle.NombreCompleto);
        Assert.Equal(beto.IdentificadorAcceso, detalle.IdentificadorAcceso);
        Assert.Equal(new RolResumen { Id = beto.Rol.Id, Nombre = beto.Rol.Nombre }, detalle.Rol);
        Assert.Equal("activo", detalle.Estado);
        Assert.False(detalle.EsCuentaPropia);

        // Acciones de ana (todas las capacidades) sobre otro usuario Activo.
        Assert.Equal(
            new AccionesPermitidas { VerDetalle = true, Editar = true, CambiarRol = true, EditarIdentificador = false, Activar = false, Desactivar = true },
            detalle.Acciones);

        // El ETag es la misma versión que el campo version.
        Assert.NotNull(respuesta.Headers.ETag);
        Assert.Equal(detalle.Version, respuesta.Headers.ETag.ToString());

        // La propia cuenta se marca y no ofrece desactivarse.
        var propia = JsonSerializer.Deserialize<DetalleUsuario>(await ana.GetStringAsync($"/api/usuarios/{SemillaDatos.Ana.Id}", ct), Json)!;
        Assert.True(propia.EsCuentaPropia);
        Assert.False(propia.Acciones.Desactivar);
    }

    [Fact]
    public async Task IdDeOtraEmpresa_EsIgualByteAByteQueUnIdInventado_YAmbosSeAuditan()
    {
        var ct = TestContext.Current.CancellationToken;
        var ana = fabrica.ClienteComo(SemillaDatos.Ana.Identidad);
        var idDeB = SemillaDatos.AnaBeta.Id;
        var antes = DateTimeOffset.UtcNow.AddSeconds(-5);

        var deB = await ana.GetAsync($"/api/usuarios/{idDeB}", ct);
        var inventado = await ana.GetAsync($"/api/usuarios/{IdInventado}", ct);

        Assert.Equal(HttpStatusCode.NotFound, deB.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inventado.StatusCode);
        var bytesDeB = await deB.Content.ReadAsByteArrayAsync(ct);
        Assert.Equal(bytesDeB, await inventado.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal(deB.Content.Headers.ContentType?.ToString(), inventado.Content.Headers.ContentType?.ToString());
        Assert.Null(deB.Headers.ETag);
        Assert.Null(inventado.Headers.ETag);

        var problema = JsonSerializer.Deserialize<Problema>(bytesDeB, Json)!;
        Assert.Equal(404, problema.Status);
        Assert.Equal(CodigosError.UsuarioNoEncontrado, problema.Codigo);

        // Ningún dato del usuario de B ni de su empresa.
        var cuerpo = System.Text.Encoding.UTF8.GetString(bytesDeB);
        foreach (var dato in new[] { idDeB.ToString(), SemillaDatos.EmpresaB.ToString(), SemillaDatos.AnaBeta.NombreCompleto, SemillaDatos.AnaBeta.IdentificadorAcceso })
        {
            Assert.DoesNotContain(dato, cuerpo, StringComparison.OrdinalIgnoreCase);
        }

        // Mismo registro para los dos: actor, empresa del actor, id solicitado, Consulta y momento; nada del recurso.
        var registros = await NoAccesiblesAsync(antes, ct);
        foreach (var id in new[] { idDeB, IdInventado })
        {
            var registro = Assert.Single(registros, r => r.Afectado == id);
            Assert.Equal(SemillaDatos.Ana.Id, registro.Actor);
            Assert.Equal(SemillaDatos.EmpresaA, registro.EmpresaId);
            Assert.Equal(OperacionSolicitada.Consulta, registro.OperacionSolicitada);
            Assert.InRange(registro.OcurridoEn, antes, DateTimeOffset.UtcNow.AddSeconds(5));
            Assert.Null(registro.CambiosJson);
        }
    }

    /// <summary>Registros <c>UsuarioNoAccesible</c> desde <paramref name="desde"/>, leídos por SQL bajo la empresa A.</summary>
    private async Task<List<RegistroNoAccesible>> NoAccesiblesAsync(DateTimeOffset desde, CancellationToken ct)
    {
        await using var conexion = new SqlConnection(new SqlConnectionStringBuilder(fabrica.CadenaConexion) { Pooling = false }.ConnectionString);
        await conexion.OpenAsync(ct);

        await using (var fijar = conexion.CreateCommand())
        {
            fijar.CommandText = "EXEC sys.sp_set_session_context @key = N'EmpresaId', @value = @EmpresaId, @read_only = 1;";
            fijar.Parameters.Add("@EmpresaId", SqlDbType.UniqueIdentifier).Value = SemillaDatos.EmpresaA;
            await fijar.ExecuteNonQueryAsync(ct);
        }

        await using var consulta = conexion.CreateCommand();
        consulta.CommandText = """
            SELECT ActorUsuarioId, EmpresaId, UsuarioAfectadoId, OcurridoEn, OperacionSolicitada, CambiosJson
            FROM dbo.AuditoriaUsuarios
            WHERE TipoOperacion = @Tipo AND OcurridoEn >= @Desde;
            """;
        consulta.Parameters.Add("@Tipo", SqlDbType.TinyInt).Value = (byte)TipoOperacion.UsuarioNoAccesible;
        consulta.Parameters.Add("@Desde", SqlDbType.DateTimeOffset).Value = desde;
        await using var lector = await consulta.ExecuteReaderAsync(ct);

        var registros = new List<RegistroNoAccesible>();
        while (await lector.ReadAsync(ct))
        {
            registros.Add(new RegistroNoAccesible(
                lector.GetGuid(0),
                lector.GetGuid(1),
                lector.GetGuid(2),
                lector.GetDateTimeOffset(3),
                lector.IsDBNull(4) ? null : (OperacionSolicitada)lector.GetByte(4),
                lector.IsDBNull(5) ? null : lector.GetString(5)));
        }

        return registros;
    }

    private sealed record RegistroNoAccesible(
        Guid Actor,
        Guid EmpresaId,
        Guid Afectado,
        DateTimeOffset OcurridoEn,
        OperacionSolicitada? OperacionSolicitada,
        string? CambiosJson);
}
