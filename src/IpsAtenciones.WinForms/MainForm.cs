using IpsAtenciones.Application;
using IpsAtenciones.Domain;
using IpsAtenciones.Infrastructure;
using Serilog;
using System.Data.Common;

namespace IpsAtenciones.WinForms;

internal sealed class MainForm : Form
{
    private static readonly Color Accent = Color.FromArgb(18, 100, 94);
    private readonly RegistrarAtencionHandler? handler;
    private readonly SqlCatalogService? catalogs;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ComboBox patient = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Paciente" };
    private readonly CheckedListBox services = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, AccessibleName = "Servicios activos" };
    private readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today, MinDate = DateTime.Today, MaxDate = DateTime.Today, AccessibleName = "Fecha de atención" };
    private readonly Button save = CreateButton("Guardar atención", primary: true);
    private readonly Button manage = CreateButton("Catálogos");
    private readonly Button retry = CreateButton("Reintentar carga");
    private readonly Button confirm = CreateButton("Consultar resultado");
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
    private bool ready;
    private bool busy;
    private Guid? pendingOperation;

    public MainForm(RegistrarAtencionHandler? handler, SqlCatalogService? catalogs)
    {
        this.handler = handler;
        this.catalogs = catalogs;
        Text = "IPS · Atenciones médicas";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(760, 530);
        MinimumSize = new Size(620, 470);
        BackColor = Color.White;
        ForeColor = Color.FromArgb(28, 44, 50);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(24), RowCount = 8 };
        foreach (var size in new[] { SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize, SizeType.AutoSize })
            layout.RowStyles.Add(new RowStyle(size, size == SizeType.Percent ? 100 : 0));

        layout.Controls.Add(new Label { Text = "Registrar atención", AutoSize = true, Font = new Font(Font.FontFamily, 22, FontStyle.Bold), Margin = new Padding(0, 0, 0, 18) }, 0, 0);
        layout.Controls.Add(Caption("Paciente"), 0, 1);
        layout.Controls.Add(patient, 0, 2);
        var dateRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 9, 0, 6) };
        dateRow.Controls.Add(Caption("Fecha de atención"));
        dateRow.Controls.Add(date);
        layout.Controls.Add(dateRow, 0, 3);
        layout.Controls.Add(Caption("Servicios activos"), 0, 4);
        layout.Controls.Add(services, 0, 5);
        layout.Controls.Add(status, 0, 6);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = true, Margin = new Padding(0, 12, 0, 0) };
        actions.Controls.AddRange([save, manage, retry, confirm]);
        layout.Controls.Add(actions, 0, 7);
        Controls.Add(layout);

        patient.DisplayMember = nameof(Paciente.Nombre);
        services.DisplayMember = nameof(ServiceOption.Text);
        patient.SelectedIndexChanged += (_, _) => UpdateControls();
        services.ItemCheck += (_, _) => BeginInvoke(UpdateControls);
        Shown += async (_, _) => await LoadCatalogsAsync();
        save.Click += async (_, _) => await SaveAsync();
        retry.Click += async (_, _) => await LoadCatalogsAsync();
        confirm.Click += async (_, _) => await ConfirmAsync();
        manage.Click += async (_, _) => await OpenCatalogsAsync();
        FormClosing += (_, args) => args.Cancel = busy || pendingOperation.HasValue &&
            MessageBox.Show(this, "El resultado de la atención sigue pendiente. ¿Quieres cerrar la ventana?", "Resultado pendiente", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes;
        FormClosed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        UpdateControls();
    }

    internal static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 6, 10, 3) };

    internal static Button CreateButton(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(96, 37), Padding = new Padding(8, 3, 8, 3) };
        if (primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.EnabledChanged += (_, _) => SetPrimaryColors(button);
            SetPrimaryColors(button);
        }
        return button;
    }

    private static void SetPrimaryColors(Button button)
    {
        button.BackColor = button.Enabled ? Accent : Color.FromArgb(224, 230, 230);
        button.ForeColor = button.Enabled ? Color.White : Color.FromArgb(64, 78, 81);
        button.FlatAppearance.BorderColor = button.BackColor;
    }

    private void UpdateControls()
    {
        var editable = ready && !busy && pendingOperation is null;
        patient.Enabled = services.Enabled = date.Enabled = editable;
        save.Enabled = editable && patient.SelectedIndex >= 0 && services.CheckedItems.Count > 0;
        manage.Enabled = catalogs is not null && !busy && pendingOperation is null;
        retry.Visible = handler is not null && pendingOperation is null;
        retry.Enabled = !busy;
        retry.Text = ready ? "Actualizar catálogos" : "Reintentar carga";
        confirm.Visible = pendingOperation.HasValue;
        confirm.Enabled = !busy;
    }

    private async Task LoadCatalogsAsync()
    {
        if (handler is null)
        {
            status.Text = "La conexión está sin configurar. Consulta las instrucciones de instalación.";
            return;
        }

        var selectedPatient = (patient.SelectedItem as Paciente)?.Id;
        var selectedServices = services.CheckedItems.Cast<ServiceOption>().Select(item => item.Id).ToHashSet();
        busy = true;
        UpdateControls();
        status.Text = "Cargando catálogos…";
        try
        {
            var patients = (await handler.ListarPacientesAsync(lifetime.Token)).ToList();
            var options = await handler.ListarServiciosAsync(lifetime.Token);
            if (IsDisposed) return;

            patient.DataSource = patients;
            patient.SelectedIndex = patients.FindIndex(item => item.Id == selectedPatient);
            services.BeginUpdate();
            try
            {
                services.Items.Clear();
                foreach (var service in options)
                    services.Items.Add(new ServiceOption(service.Id, $"{service.Codigo} · {service.Nombre}"), selectedServices.Contains(service.Id));
            }
            finally
            {
                services.EndUpdate();
            }
            ready = true;
            status.Text = patients.Count == 0 || options.Count == 0
                ? "Crea un paciente y un servicio activos en Catálogos para registrar una atención."
                : "Selecciona un paciente y al menos un servicio.";
        }
        catch (OperationCanceledException) when (IsDisposed) { }
        catch (Exception error)
        {
            Log.Warning("Falló la carga de catálogos ({ErrorType})", error.GetType().Name);
            ready = false;
            status.Text = "No pudimos cargar los catálogos. Revisa la conexión y vuelve a intentarlo.";
        }
        finally
        {
            busy = false;
            if (!IsDisposed) UpdateControls();
        }
    }

    private async Task OpenCatalogsAsync()
    {
        if (catalogs is null || busy || pendingOperation.HasValue) return;
        using var form = new CatalogForm(catalogs, catalogs);
        form.ShowDialog(this);
        await LoadCatalogsAsync();
    }

    private async Task SaveAsync()
    {
        if (handler is null || !save.Enabled || patient.SelectedItem is not Paciente selected) return;
        var operation = Guid.NewGuid();
        var command = new RegistrarAtencionCommand(selected.Id, DateOnly.FromDateTime(date.Value),
            services.CheckedItems.Cast<ServiceOption>().Select(item => item.Id).ToArray(), operation);
        await RunRegistrationAsync(() => handler.HandleAsync(command, lifetime.Token), operation, "Guardando atención…");
    }

    private async Task ConfirmAsync()
    {
        if (handler is null || busy || pendingOperation is not Guid operation) return;
        await RunRegistrationAsync(() => handler.ConsultarPorOperacionAsync(operation, lifetime.Token), operation, "Consultando resultado…");
    }

    private async Task RunRegistrationAsync(Func<Task<RegistrarAtencionResult>> action, Guid operation, string progress)
    {
        busy = true;
        UpdateControls();
        status.Text = progress;
        try
        {
            var result = await action();
            if (IsDisposed) return;
            if (result.CodigoResultado == ResultadoAtencion.UnknownOutcome)
                pendingOperation = operation;
            else if (result.Exitoso)
            {
                pendingOperation = null;
                patient.SelectedIndex = -1;
                foreach (var index in services.CheckedIndices.Cast<int>().ToArray())
                    services.SetItemChecked(index, false);
            }
            else pendingOperation = null;

            if (result.CodigoResultado is ResultadoAtencion.DatabaseUnavailable or ResultadoAtencion.PatientInactive or
                ResultadoAtencion.ServiceInactive or ResultadoAtencion.ReferenceNotFound)
                ready = false;

            if (!result.Exitoso)
                Log.Information("Registro de atención {OperacionId}: {Resultado}", operation, result.CodigoResultado);
            status.Text = RegistrationMessage(result);
        }
        catch (OperationCanceledException) when (IsDisposed) { }
        catch (DbException error)
        {
            Log.Warning("Falló la conexión de la operación {OperacionId} ({ErrorType})", operation, error.GetType().Name);
            ready = false;
            status.Text = "No pudimos conectar con la base de datos. Revisa la conexión y vuelve a intentarlo.";
        }
        catch (Exception error)
        {
            Log.Error("Falló la operación de atención {OperacionId} ({ErrorType})", operation, error.GetType().Name);
            status.Text = pendingOperation.HasValue
                ? "Aún no podemos confirmar el registro. Consulta el resultado cuando se recupere la conexión."
                : $"No pudimos completar el registro. Referencia: {operation}.";
        }
        finally
        {
            busy = false;
            if (!IsDisposed) UpdateControls();
        }
    }

    private static string RegistrationMessage(RegistrarAtencionResult result) => result.CodigoResultado switch
    {
        ResultadoAtencion.Ok => $"Atención registrada. Número {result.AtencionId}.",
        ResultadoAtencion.DuplicateAttention => "Ya existe una atención con ese paciente, fecha y servicio. Revisa los servicios marcados.",
        ResultadoAtencion.UnknownOutcome => $"El registro aún no se puede confirmar. Consulta el resultado. Referencia: {result.CorrelationId}.",
        ResultadoAtencion.DatabaseUnavailable => "No pudimos conectar con la base de datos. Revisa la conexión y vuelve a intentarlo.",
        ResultadoAtencion.PatientRequired or ResultadoAtencion.ServiceRequired => "Selecciona un paciente y al menos un servicio.",
        ResultadoAtencion.PatientInactive or ResultadoAtencion.ServiceInactive or ResultadoAtencion.ReferenceNotFound => "El paciente o un servicio dejó de estar disponible. Actualiza los catálogos.",
        ResultadoAtencion.ServiceDuplicate => "Un servicio está repetido. Revisa la selección.",
        ResultadoAtencion.ValidationError => "Selecciona una fecha de atención válida.",
        _ => $"No pudimos completar el registro. Referencia: {result.CorrelationId}."
    };

    private sealed record ServiceOption(int Id, string Text);
}
