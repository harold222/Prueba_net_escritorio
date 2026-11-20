using IpsAtenciones.Domain;

namespace IpsAtenciones.Application;

public sealed record RegistrarAtencionCommand(
    int PacienteId,
    DateOnly FechaAtencion,
    IReadOnlyCollection<int> Servicios,
    Guid OperacionId);

public sealed record RegistrarAtencionResult(
    bool Exitoso,
    ResultadoAtencion CodigoResultado,
    int? AtencionId,
    Guid CorrelationId)
{
    public static RegistrarAtencionResult Rechazo(ResultadoAtencion codigo, Guid id) =>
        new(false, codigo, null, id);
}

public sealed class CatalogDuplicateException : Exception;
public sealed class CatalogValidationException : Exception;

public interface IPacienteQuery
{
    Task<IReadOnlyList<Paciente>> ListarAsync(CancellationToken cancellationToken);
}

public interface IServicioMedicoQuery
{
    Task<IReadOnlyList<ServicioMedico>> ListarActivosAsync(CancellationToken cancellationToken);
}

public interface IPacienteCatalogService
{
    Task<IReadOnlyList<Paciente>> ListarAsync(CancellationToken cancellationToken);
    Task CrearAsync(Paciente paciente, CancellationToken cancellationToken);
    Task ActualizarAsync(Paciente paciente, CancellationToken cancellationToken);
    Task DesactivarAsync(int id, CancellationToken cancellationToken);
}

public interface IServicioCatalogService
{
    Task<IReadOnlyList<ServicioMedico>> ListarAsync(CancellationToken cancellationToken);
    Task CrearAsync(ServicioMedico servicio, CancellationToken cancellationToken);
    Task ActualizarAsync(ServicioMedico servicio, CancellationToken cancellationToken);
    Task DesactivarAsync(int id, CancellationToken cancellationToken);
}

public interface IAttentionRepository
{
    Task<RegistrarAtencionResult> RegistrarAsync(RegistrarAtencionCommand command, CancellationToken cancellationToken);
    Task<RegistrarAtencionResult> ConsultarPorOperacionAsync(Guid operacionId, CancellationToken cancellationToken);
}
