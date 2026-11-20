using IpsAtenciones.Application;
using IpsAtenciones.Domain;
using IpsAtenciones.Infrastructure;
using Microsoft.Data.SqlClient;
using System.Data;
using Xunit;

namespace IpsAtenciones.IntegrationTests;

public sealed class SqlAttentionRepositoryTests
{
    private static string ConnectionString => Environment.GetEnvironmentVariable("ConnectionStrings__IpsAtenciones")
        ?? throw new InvalidOperationException("Configure ConnectionStrings__IpsAtenciones for an isolated SQL test database.");

    [Fact]
    public async Task ConsultaOperacionInexistenteDevuelveResultadoDesconocido()
    {
        var result = await new SqlAttentionRepository(ConnectionString).ConsultarPorOperacionAsync(Guid.NewGuid(), default);
        Assert.Equal(ResultadoAtencion.UnknownOutcome, result.CodigoResultado);
    }

    [Fact]
    public async Task CancelacionAntesDeAbrirLaConexionNoPersiste()
    {
        var repository = new SqlAttentionRepository(ConnectionString);
        var command = await CreateCommandAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.RegistrarAsync(command, cancellation.Token));
        Assert.Equal(ResultadoAtencion.UnknownOutcome, (await repository.ConsultarPorOperacionAsync(command.OperacionId, default)).CodigoResultado);
    }

    [Fact]
    public async Task CancelacionMientrasElRegistroEsperaUnBloqueoNoCreaAtencion()
    {
        var patient = await CreatePatientAsync();
        var request = await CreateCommandAsync(patient);
        var repository = new SqlAttentionRepository(ConnectionString);
        await using var blocker = new SqlConnection(ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = (SqlTransaction)await blocker.BeginTransactionAsync();
        await using var update = new SqlCommand("dbo.usp_Paciente_Actualizar", blocker, transaction) { CommandType = CommandType.StoredProcedure };
        update.Parameters.Add("@PacienteId", SqlDbType.Int).Value = patient.Id;
        update.Parameters.Add("@TipoDocumento", SqlDbType.VarChar, 20).Value = patient.TipoDocumento;
        update.Parameters.Add("@NumeroDocumento", SqlDbType.VarChar, 40).Value = patient.NumeroDocumento;
        update.Parameters.Add("@Nombre", SqlDbType.NVarChar, 160).Value = patient.Nombre + " bloqueado";
        await update.ExecuteNonQueryAsync();

        using var cancellation = new CancellationTokenSource();
        var registration = repository.RegistrarAsync(request, cancellation.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(registration.IsCompleted);
        await cancellation.CancelAsync();
        var outcome = await registration.WaitAsync(TimeSpan.FromSeconds(15));
        await transaction.RollbackAsync();

        Assert.Equal(ResultadoAtencion.UnknownOutcome, outcome.CodigoResultado);
        Assert.Equal(request.OperacionId, outcome.CorrelationId);
        Assert.Equal(0, await HistoryCountAsync(request));
    }

    [Fact]
    public async Task RepetirOperacionDevuelveLaMismaAtencion()
    {
        var repository = new SqlAttentionRepository(ConnectionString);
        var command = await CreateCommandAsync();
        var registered = await repository.RegistrarAsync(command, default);
        var retried = await repository.RegistrarAsync(command, default);
        var queried = await repository.ConsultarPorOperacionAsync(command.OperacionId, default);

        Assert.Equal(ResultadoAtencion.Ok, registered.CodigoResultado);
        Assert.NotNull(registered.AtencionId);
        Assert.Equal(registered, retried);
        Assert.Equal(registered, queried);
        Assert.Equal(1, await HistoryCountAsync(command));
    }

    [Fact]
    public async Task DuplicidadConsecutivaDevuelveRechazoConsultable()
    {
        var repository = new SqlAttentionRepository(ConnectionString);
        var first = await CreateCommandAsync();
        var second = first with { OperacionId = Guid.NewGuid() };

        Assert.Equal(ResultadoAtencion.Ok, (await repository.RegistrarAsync(first, default)).CodigoResultado);
        var rejected = await repository.RegistrarAsync(second, default);
        Assert.Equal(ResultadoAtencion.DuplicateAttention, rejected.CodigoResultado);
        Assert.Null(rejected.AtencionId);
        Assert.Equal(rejected, await repository.ConsultarPorOperacionAsync(second.OperacionId, default));
        Assert.Equal(1, await HistoryCountAsync(first));
    }

    [Fact]
    public async Task DuplicidadConcurrenteAceptaUnaSolaAtencion()
    {
        var repository = new SqlAttentionRepository(ConnectionString);
        var first = await CreateCommandAsync();
        var second = first with { OperacionId = Guid.NewGuid() };

        var results = await Task.WhenAll(repository.RegistrarAsync(first, default), repository.RegistrarAsync(second, default));

        Assert.Single(results, result => result.CodigoResultado == ResultadoAtencion.Ok);
        Assert.Single(results, result => result.CodigoResultado == ResultadoAtencion.DuplicateAttention);
        Assert.Equal(1, await HistoryCountAsync(first));
    }

    [Fact]
    public async Task RechazaSinServiciosOSiElLoteContieneUnaReferenciaInvalida()
    {
        var repository = new SqlAttentionRepository(ConnectionString);
        var command = await CreateCommandAsync();
        var empty = command with { Servicios = [] };
        var invalid = command with { Servicios = [.. command.Servicios, int.MaxValue], OperacionId = Guid.NewGuid() };

        var missing = await repository.RegistrarAsync(empty, default);
        Assert.Equal(ResultadoAtencion.ServiceRequired, missing.CodigoResultado);
        Assert.Null(missing.AtencionId);
        Assert.Equal(missing, await repository.ConsultarPorOperacionAsync(empty.OperacionId, default));
        Assert.Equal(ResultadoAtencion.ReferenceNotFound, (await repository.RegistrarAsync(invalid, default)).CodigoResultado);
        Assert.Equal(0, await HistoryCountAsync(command));
        Assert.Equal(ResultadoAtencion.Ok, (await repository.RegistrarAsync(command with { OperacionId = Guid.NewGuid() }, default)).CodigoResultado);
    }

    [Fact]
    public async Task CatalogoDeServiciosValidaActualizaYDesactiva()
    {
        IServicioCatalogService catalogs = new SqlCatalogService(ConnectionString);
        var code = NewCode();
        await catalogs.CrearAsync(new ServicioMedico(0, $" {code.ToLowerInvariant()} ", "Servicio de prueba", true), default);
        var item = Assert.Single(await catalogs.ListarAsync(default), item => item.Codigo == code);

        await Assert.ThrowsAsync<CatalogDuplicateException>(() => catalogs.CrearAsync(item, default));
        await Assert.ThrowsAsync<CatalogValidationException>(() => catalogs.CrearAsync(item with { Codigo = "" }, default));
        await catalogs.ActualizarAsync(item with { Nombre = "Servicio actualizado" }, default);
        await catalogs.DesactivarAsync(item.Id, default);

        var updated = Assert.Single(await catalogs.ListarAsync(default), candidate => candidate.Id == item.Id);
        Assert.Equal("Servicio actualizado", updated.Nombre);
        Assert.False(updated.Activo);
        Assert.DoesNotContain(await new SqlServicioQuery(ConnectionString).ListarActivosAsync(default), candidate => candidate.Id == item.Id);

        var repository = new SqlAttentionRepository(ConnectionString);
        var command = (await CreateCommandAsync()) with { Servicios = [item.Id] };
        Assert.Equal(ResultadoAtencion.ServiceInactive, (await repository.RegistrarAsync(command, default)).CodigoResultado);
        Assert.Equal(0, await HistoryCountAsync(command));
    }

    [Fact]
    public async Task CatalogoDePacientesConservaDocumentoYPermiteDesactivar()
    {
        IPacienteCatalogService catalogs = new SqlCatalogService(ConnectionString);
        var item = await CreatePatientAsync();
        await Assert.ThrowsAsync<CatalogDuplicateException>(() => catalogs.CrearAsync(item, default));
        await Assert.ThrowsAsync<CatalogValidationException>(() => catalogs.ActualizarAsync(item with { NumeroDocumento = NewCode() }, default));
        await catalogs.ActualizarAsync(item with { Nombre = "Paciente actualizado", FechaNacimiento = new DateOnly(1990, 1, 1) }, default);
        await catalogs.DesactivarAsync(item.Id, default);

        var updated = Assert.Single(await catalogs.ListarAsync(default), candidate => candidate.Id == item.Id);
        Assert.Equal(item.NumeroDocumento, updated.NumeroDocumento);
        Assert.Equal("Paciente actualizado", updated.Nombre);
        Assert.Equal(new DateOnly(1990, 1, 1), updated.FechaNacimiento);
        Assert.False(updated.Activo);
    }

    private static string NewCode() => $"QA{Guid.NewGuid():N}"[..18].ToUpperInvariant();

    private static async Task<Paciente> CreatePatientAsync()
    {
        IPacienteCatalogService catalogs = new SqlCatalogService(ConnectionString);
        var document = NewCode();
        await catalogs.CrearAsync(new Paciente(0, " cc ", $" {document.ToLowerInvariant()} ", "Paciente de prueba", null, true), default);
        return Assert.Single(await catalogs.ListarAsync(default), item => item.NumeroDocumento == document);
    }

    private static async Task<RegistrarAtencionCommand> CreateCommandAsync(Paciente? patient = null)
    {
        patient ??= await CreatePatientAsync();
        var services = await new SqlServicioQuery(ConnectionString).ListarActivosAsync(default);
        var service = Assert.Single(services, item => item.Codigo == "CONS");
        return new RegistrarAtencionCommand(patient.Id, DateOnly.FromDateTime(DateTime.Today), [service.Id], Guid.NewGuid());
    }

    private static async Task<int> HistoryCountAsync(RegistrarAtencionCommand command)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var sql = new SqlCommand("dbo.usp_Atencion_Listar", connection) { CommandType = CommandType.StoredProcedure };
        sql.Parameters.Add("@PacienteId", SqlDbType.Int).Value = command.PacienteId;
        sql.Parameters.Add("@Desde", SqlDbType.Date).Value = command.FechaAtencion.ToDateTime(TimeOnly.MinValue);
        sql.Parameters.Add("@Hasta", SqlDbType.Date).Value = command.FechaAtencion.ToDateTime(TimeOnly.MinValue);
        await using var reader = await sql.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync()) count++;
        return count;
    }
}
