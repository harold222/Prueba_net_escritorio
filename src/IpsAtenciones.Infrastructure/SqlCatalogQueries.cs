using System.Data;
using IpsAtenciones.Application;
using IpsAtenciones.Domain;
using Microsoft.Data.SqlClient;

namespace IpsAtenciones.Infrastructure;

public sealed class SqlPacienteQuery(string connectionString) : IPacienteQuery
{
    public async Task<IReadOnlyList<Paciente>> ListarAsync(CancellationToken cancellationToken)
    {
        var patients = new List<Paciente>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.usp_Paciente_Listar", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var birthDate = reader.IsDBNull(4) ? (DateOnly?)null : DateOnly.FromDateTime(reader.GetDateTime(4));
            patients.Add(new Paciente(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), birthDate, reader.GetBoolean(5)));
        }
        return patients;
    }
}

public sealed class SqlServicioQuery(string connectionString) : IServicioMedicoQuery
{
    public Task<IReadOnlyList<ServicioMedico>> ListarActivosAsync(CancellationToken cancellationToken) =>
        ListarAsync("dbo.usp_ServicioMedico_ListarActivos", cancellationToken);

    internal Task<IReadOnlyList<ServicioMedico>> ListarTodosAsync(CancellationToken cancellationToken) =>
        ListarAsync("dbo.usp_ServicioMedico_Listar", cancellationToken);

    private async Task<IReadOnlyList<ServicioMedico>> ListarAsync(string procedure, CancellationToken cancellationToken)
    {
        var services = new List<ServicioMedico>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            services.Add(new ServicioMedico(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        }
        return services;
    }
}

public sealed class SqlCatalogService(string connectionString) : IPacienteCatalogService, IServicioCatalogService
{
    private readonly SqlPacienteQuery patientQuery = new(connectionString);
    private readonly SqlServicioQuery serviceQuery = new(connectionString);

    public Task<IReadOnlyList<Paciente>> ListarAsync(CancellationToken cancellationToken) =>
        patientQuery.ListarAsync(cancellationToken);

    Task<IReadOnlyList<ServicioMedico>> IServicioCatalogService.ListarAsync(CancellationToken cancellationToken) =>
        serviceQuery.ListarTodosAsync(cancellationToken);

    public Task CrearAsync(Paciente paciente, CancellationToken cancellationToken) =>
        ExecuteAsync("dbo.usp_Paciente_Crear", cancellationToken,
            Text("@TipoDocumento", SqlDbType.VarChar, 20, paciente.TipoDocumento),
            Text("@NumeroDocumento", SqlDbType.VarChar, 40, paciente.NumeroDocumento),
            Text("@Nombre", SqlDbType.NVarChar, 160, paciente.Nombre),
            BirthDate(paciente.FechaNacimiento));

    public Task ActualizarAsync(Paciente paciente, CancellationToken cancellationToken) =>
        ExecuteAsync("dbo.usp_Paciente_Actualizar", cancellationToken,
            Id("@PacienteId", paciente.Id),
            Text("@TipoDocumento", SqlDbType.VarChar, 20, paciente.TipoDocumento),
            Text("@NumeroDocumento", SqlDbType.VarChar, 40, paciente.NumeroDocumento),
            Text("@Nombre", SqlDbType.NVarChar, 160, paciente.Nombre),
            BirthDate(paciente.FechaNacimiento));

    public Task DesactivarAsync(int id, CancellationToken cancellationToken) =>
        ExecuteAsync("dbo.usp_Paciente_Desactivar", cancellationToken, Id("@PacienteId", id));

    public Task CrearAsync(ServicioMedico servicio, CancellationToken cancellationToken) =>
        ExecuteAsync("dbo.usp_ServicioMedico_Crear", cancellationToken,
            Text("@Codigo", SqlDbType.VarChar, 30, servicio.Codigo),
            Text("@Nombre", SqlDbType.NVarChar, 160, servicio.Nombre));

    public Task ActualizarAsync(ServicioMedico servicio, CancellationToken cancellationToken) =>
        ExecuteAsync("dbo.usp_ServicioMedico_Actualizar", cancellationToken,
            Id("@ServicioMedicoId", servicio.Id),
            Text("@Codigo", SqlDbType.VarChar, 30, servicio.Codigo),
            Text("@Nombre", SqlDbType.NVarChar, 160, servicio.Nombre));

    Task IServicioCatalogService.DesactivarAsync(int id, CancellationToken cancellationToken) =>
        ExecuteAsync("dbo.usp_ServicioMedico_Desactivar", cancellationToken, Id("@ServicioMedicoId", id));

    private async Task ExecuteAsync(string procedure, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
        command.Parameters.AddRange(parameters);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            throw new CatalogDuplicateException();
        }
        catch (SqlException error) when (error.Number is 50001 or 50002)
        {
            throw new CatalogValidationException();
        }
    }

    private static SqlParameter Id(string name, int id) => new(name, SqlDbType.Int) { Value = id };

    private static SqlParameter BirthDate(DateOnly? date) => new("@FechaNacimiento", SqlDbType.Date)
    {
        Value = date is { } value ? value.ToDateTime(TimeOnly.MinValue) : DBNull.Value
    };

    private static SqlParameter Text(string name, SqlDbType type, int size, string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > size)
            throw new CatalogValidationException();
        return new SqlParameter(name, type, size) { Value = normalized };
    }
}
