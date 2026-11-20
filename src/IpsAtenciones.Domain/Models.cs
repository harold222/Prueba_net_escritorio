namespace IpsAtenciones.Domain;

public enum ResultadoAtencion
{
    Ok,
    PatientRequired,
    PatientInactive,
    ServiceRequired,
    ServiceDuplicate,
    ServiceInactive,
    ReferenceNotFound,
    DuplicateAttention,
    ValidationError,
    DatabaseUnavailable,
    UnexpectedError,
    UnknownOutcome
}

public sealed record Paciente(
    int Id,
    string TipoDocumento,
    string NumeroDocumento,
    string Nombre,
    DateOnly? FechaNacimiento,
    bool Activo);

public sealed record ServicioMedico(int Id, string Codigo, string Nombre, bool Activo);

public sealed record Atencion(int PacienteId, DateOnly Fecha, IReadOnlyCollection<int> Servicios)
{
    public static bool EsValida(Atencion atencion) =>
        atencion.PacienteId > 0 &&
        atencion.Servicios.Count > 0 &&
        atencion.Servicios.Distinct().Count() == atencion.Servicios.Count;
}
