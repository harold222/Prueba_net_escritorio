using IpsAtenciones.Domain;

namespace IpsAtenciones.Application;

public sealed class RegistrarAtencionHandler(
    IPacienteQuery pacientes,
    IServicioMedicoQuery servicios,
    IAttentionRepository repository)
{
    public async Task<RegistrarAtencionResult> HandleAsync(
        RegistrarAtencionCommand command, CancellationToken cancellationToken)
    {
        var operation = command.OperacionId == Guid.Empty ? Guid.NewGuid() : command.OperacionId;
        if (command.PacienteId <= 0)
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.PatientRequired, operation);
        if (command.Servicios is not { Count: > 0 })
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.ServiceRequired, operation);
        if (command.Servicios.Count != command.Servicios.Distinct().Count())
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.ServiceDuplicate, operation);
        if (command.FechaAtencion != DateOnly.FromDateTime(DateTime.Today))
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.ValidationError, operation);

        var paciente = (await pacientes.ListarAsync(cancellationToken)).FirstOrDefault(item => item.Id == command.PacienteId);
        if (paciente is null)
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.ReferenceNotFound, operation);
        if (!paciente.Activo)
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.PatientInactive, operation);

        var serviciosActivos = (await servicios.ListarActivosAsync(cancellationToken)).Select(item => item.Id).ToHashSet();
        if (command.Servicios.Any(id => !serviciosActivos.Contains(id)))
            return RegistrarAtencionResult.Rechazo(ResultadoAtencion.ServiceInactive, operation);

        return await repository.RegistrarAsync(command with { OperacionId = operation }, cancellationToken);
    }

    public async Task<IReadOnlyList<Paciente>> ListarPacientesAsync(CancellationToken cancellationToken) =>
        (await pacientes.ListarAsync(cancellationToken)).Where(item => item.Activo).ToArray();

    public Task<IReadOnlyList<ServicioMedico>> ListarServiciosAsync(CancellationToken cancellationToken) =>
        servicios.ListarActivosAsync(cancellationToken);

    public Task<RegistrarAtencionResult> ConsultarPorOperacionAsync(Guid operacionId, CancellationToken cancellationToken) =>
        repository.ConsultarPorOperacionAsync(operacionId, cancellationToken);
}
