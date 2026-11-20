using IpsAtenciones.Application;
using IpsAtenciones.Domain;
namespace IpsAtenciones.UnitTests;

public sealed class HandlerTests
{
    private sealed class ActivePatientQuery : IPacienteQuery
    {
        public Task<IReadOnlyList<Paciente>> ListarAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<Paciente> pacientes =
            [
                new Paciente(1, "CC", "1", "Ana", null, true)
            ];

            return Task.FromResult(pacientes);
        }
    }

    private sealed class ActiveServiceQuery : IServicioMedicoQuery
    {
        public Task<IReadOnlyList<ServicioMedico>> ListarActivosAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ServicioMedico> servicios =
            [
                new ServicioMedico(2, "CONS", "Consulta", true)
            ];

            return Task.FromResult(servicios);
        }
    }

    private sealed class RecordingAttentionRepository : IAttentionRepository
    {
        public RegistrarAtencionCommand? Last { get; private set; }

        public Task<RegistrarAtencionResult> RegistrarAsync(
            RegistrarAtencionCommand command,
            CancellationToken cancellationToken)
        {
            Last = command;
            return Task.FromResult(
                new RegistrarAtencionResult(
                    true,
                    ResultadoAtencion.Ok,
                    9,
                    command.OperacionId));
        }

        public Task<RegistrarAtencionResult> ConsultarPorOperacionAsync(Guid operacionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(RegistrarAtencionResult.Rechazo(ResultadoAtencion.UnknownOutcome, operacionId));
        }
    }
    [Theory]
    [InlineData(0, 0, new[] { 2 }, ResultadoAtencion.PatientRequired)]
    [InlineData(1, 0, new[] { 2, 2 }, ResultadoAtencion.ServiceDuplicate)]
    [InlineData(1, 0, new int[0], ResultadoAtencion.ServiceRequired)]
    [InlineData(1, 1, new[] { 2 }, ResultadoAtencion.ValidationError)]
    public async Task RechazaComandosInvalidosSinPersistir(
        int patientId, int dayOffset, int[] serviceIds, ResultadoAtencion expected)
    {
        var repository = new RecordingAttentionRepository();
        var handler = new RegistrarAtencionHandler(new ActivePatientQuery(), new ActiveServiceQuery(), repository);
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(dayOffset));

        var result = await handler.HandleAsync(new(patientId, date, serviceIds, Guid.NewGuid()), default);

        Assert.Equal(expected, result.CodigoResultado);
        Assert.Null(repository.Last);
    }

    [Fact]
    public async Task PersisteComandoValido()
    {
        var repository = new RecordingAttentionRepository();
        var handler = new RegistrarAtencionHandler(new ActivePatientQuery(), new ActiveServiceQuery(), repository);
        var command = new RegistrarAtencionCommand(1, DateOnly.FromDateTime(DateTime.Today), [2], Guid.NewGuid());

        var result = await handler.HandleAsync(command, default);

        Assert.Equal(ResultadoAtencion.Ok, result.CodigoResultado);
        Assert.Equal(command, repository.Last);
    }

    [Fact]
    public async Task RechazaServicioInactivo()
    {
        var repository = new RecordingAttentionRepository();
        var handler = new RegistrarAtencionHandler(new ActivePatientQuery(), new EmptyActiveServiceQuery(), repository);
        var command = new RegistrarAtencionCommand(1, DateOnly.FromDateTime(DateTime.Today), [2], Guid.NewGuid());

        var result = await handler.HandleAsync(command, default);

        Assert.Equal(ResultadoAtencion.ServiceInactive, result.CodigoResultado);
        Assert.Null(repository.Last);
    }

    [Fact]
    public async Task RechazaPacienteInactivo()
    {
        var repository = new RecordingAttentionRepository();
        var handler = new RegistrarAtencionHandler(new InactivePatientQuery(), new ActiveServiceQuery(), repository);
        var command = new RegistrarAtencionCommand(1, DateOnly.FromDateTime(DateTime.Today), [2], Guid.NewGuid());

        var result = await handler.HandleAsync(command, default);

        Assert.Equal(ResultadoAtencion.PatientInactive, result.CodigoResultado);
        Assert.Null(repository.Last);
    }

    [Fact]
    public async Task GeneraCorrelacionCuandoOperacionEsVacia()
    {
        var repository = new RecordingAttentionRepository();
        var handler = new RegistrarAtencionHandler(new ActivePatientQuery(), new ActiveServiceQuery(), repository);
        var command = new RegistrarAtencionCommand(1, DateOnly.FromDateTime(DateTime.Today), [2], Guid.Empty);

        var result = await handler.HandleAsync(command, default);

        Assert.NotEqual(Guid.Empty, result.CorrelationId);
        Assert.Equal(result.CorrelationId, repository.Last?.OperacionId);
    }

    [Fact]
    public async Task ExcluyePacientesInactivosDeRegistro()
    {
        var handler = new RegistrarAtencionHandler(new InactivePatientQuery(), new ActiveServiceQuery(), new RecordingAttentionRepository());
        Assert.Empty(await handler.ListarPacientesAsync(default));
        Assert.Single(await handler.ListarServiciosAsync(default));
    }

    [Fact]
    public async Task RechazaPacienteInexistenteSinPersistir()
    {
        var repository = new RecordingAttentionRepository();
        var handler = new RegistrarAtencionHandler(new ActivePatientQuery(), new ActiveServiceQuery(), repository);
        var result = await handler.HandleAsync(new(99, DateOnly.FromDateTime(DateTime.Today), [2], Guid.NewGuid()), default);

        Assert.Equal(ResultadoAtencion.ReferenceNotFound, result.CodigoResultado);
        Assert.Null(repository.Last);
    }

    [Fact]
    public async Task ConsultarOperacionConservaElCorrelativo()
    {
        var handler = new RegistrarAtencionHandler(new ActivePatientQuery(), new ActiveServiceQuery(), new RecordingAttentionRepository());
        var operation = Guid.NewGuid();
        var result = await handler.ConsultarPorOperacionAsync(operation, default);

        Assert.Equal(operation, result.CorrelationId);
        Assert.Equal(ResultadoAtencion.UnknownOutcome, result.CodigoResultado);
    }

    [Theory]
    [InlineData(1, 2, 3, true)]
    [InlineData(0, 2, 3, false)]
    [InlineData(1, 2, 2, false)]
    public void DominioValidaPacienteYServiciosUnicos(int patient, int first, int second, bool expected)
    {
        var attention = new Atencion(patient, DateOnly.FromDateTime(DateTime.Today), [first, second]);
        Assert.Equal(expected, Atencion.EsValida(attention));
        Assert.False(Atencion.EsValida(attention with { Servicios = [] }));
    }
    private sealed class EmptyActiveServiceQuery : IServicioMedicoQuery
    {
        public Task<IReadOnlyList<ServicioMedico>> ListarActivosAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ServicioMedico>>([]);
        }
    }

    private sealed class InactivePatientQuery : IPacienteQuery
    {
        public Task<IReadOnlyList<Paciente>> ListarAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<Paciente> pacientes =
            [
                new Paciente(1, "CC", "1", "Ana", null, false)
            ];

            return Task.FromResult(pacientes);
        }
    }
}
