using System.Data;
using IpsAtenciones.Application;
using IpsAtenciones.Domain;
using Microsoft.Data.SqlClient;

namespace IpsAtenciones.Infrastructure;

public sealed class SqlAttentionRepository(string connectionString) : IAttentionRepository
{
    public async Task<RegistrarAtencionResult> RegistrarAsync(RegistrarAtencionCommand command, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
        }
        catch (SqlException) when (!ct.IsCancellationRequested)
        {
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.DatabaseUnavailable, command.OperacionId);
        }

        await using var cmd = new SqlCommand("dbo.usp_Atencion_Registrar", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
        };
        cmd.Parameters.Add("@PacienteId", SqlDbType.Int).Value = command.PacienteId;
        cmd.Parameters.Add("@FechaAtencion", SqlDbType.Date).Value = command.FechaAtencion.ToDateTime(TimeOnly.MinValue);
        cmd.Parameters.Add("@OperacionId", SqlDbType.UniqueIdentifier).Value = command.OperacionId;
        using var tvp = new DataTable();
        tvp.Columns.Add("ServicioMedicoId", typeof(int));
        foreach (var id in command.Servicios)
            tvp.Rows.Add(id);
        cmd.Parameters.Add(new SqlParameter("@Servicios", tvp) { SqlDbType = SqlDbType.Structured, TypeName = "dbo.AtencionServicioIdList" });
        ct.ThrowIfCancellationRequested();
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            if (!await reader.ReadAsync(ct))
            {
                return await ConsultarPorOperacionAsync(command.OperacionId, CancellationToken.None);
            }

            var code = ParseResult(reader.GetString(0));
            return new(code == ResultadoAtencion.Ok, code, reader.IsDBNull(1) ? null : reader.GetInt32(1), command.OperacionId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return await ConsultarPorOperacionAsync(command.OperacionId, CancellationToken.None);
        }
        catch (SqlException ex) when (!ct.IsCancellationRequested && (ex.Number is 2601 or 2627))
        {
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.DuplicateAttention, command.OperacionId);
        }
        catch (SqlException ex) when (!ct.IsCancellationRequested && ex.Number is 1205 or 229)
        {
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.DatabaseUnavailable, command.OperacionId);
        }
        catch (SqlException)
        {
            return await ConsultarPorOperacionAsync(command.OperacionId, CancellationToken.None);
        }
    }

    public async Task<RegistrarAtencionResult> ConsultarPorOperacionAsync(Guid operacionId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await using var command = new SqlCommand("dbo.usp_Atencion_ConsultarPorOperacion", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 10
        };
        command.Parameters.Add("@OperacionId", SqlDbType.UniqueIdentifier).Value = operacionId;

        try
        {
            await connection.OpenAsync(ct);
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
            if (await reader.ReadAsync(ct))
            {
                var code = ParseResult(reader.GetString(0));
                return new(code == ResultadoAtencion.Ok, code, reader.IsDBNull(1) ? null : reader.GetInt32(1), operacionId);
            }
        }
        catch (SqlException) when (!ct.IsCancellationRequested)
        {
            // The original attempt may have committed before the connection was lost.
        }

        return RegistrarAtencionResult.Rechazo(ResultadoAtencion.UnknownOutcome, operacionId);
    }

    private static ResultadoAtencion ParseResult(string value) => value.ToUpperInvariant() switch
    {
        "OK" => ResultadoAtencion.Ok,
        "PATIENT_REQUIRED" => ResultadoAtencion.PatientRequired,
        "PATIENT_INACTIVE" => ResultadoAtencion.PatientInactive,
        "SERVICE_REQUIRED" => ResultadoAtencion.ServiceRequired,
        "SERVICE_DUPLICATE" => ResultadoAtencion.ServiceDuplicate,
        "SERVICE_INACTIVE" => ResultadoAtencion.ServiceInactive,
        "REFERENCE_NOT_FOUND" => ResultadoAtencion.ReferenceNotFound,
        "DUPLICATE_ATTENTION" => ResultadoAtencion.DuplicateAttention,
        "VALIDATION_ERROR" => ResultadoAtencion.ValidationError,
        "DATABASE_UNAVAILABLE" => ResultadoAtencion.DatabaseUnavailable,
        "UNEXPECTED_ERROR" => ResultadoAtencion.UnexpectedError,
        _ => ResultadoAtencion.UnknownOutcome
    };
}
