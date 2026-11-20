using IpsAtenciones.Application;
using IpsAtenciones.Domain;
using Serilog;

namespace IpsAtenciones.WinForms;

internal sealed class CatalogForm : Form
{
    private readonly IPacienteCatalogService patients;
    private readonly IServicioCatalogService services;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ListBox patientList = new() { Dock = DockStyle.Fill, FormattingEnabled = true, AccessibleName = "Lista de pacientes" };
    private readonly ListBox serviceList = new() { Dock = DockStyle.Fill, FormattingEnabled = true, AccessibleName = "Lista de servicios médicos" };
    private readonly TextBox documentType = CreateInput("Tipo de documento", 20);
    private readonly TextBox documentNumber = CreateInput("Número de documento", 40);
    private readonly TextBox patientName = CreateInput("Nombre del paciente", 160);
    private readonly DateTimePicker birth = new() { ShowCheckBox = true, Checked = false, Format = DateTimePickerFormat.Short, AccessibleName = "Fecha de nacimiento opcional" };
    private readonly TextBox serviceCode = CreateInput("Código del servicio", 30);
    private readonly TextBox serviceName = CreateInput("Nombre del servicio", 160);
    private readonly Button deactivatePatient = MainForm.CreateButton("Desactivar");
    private readonly Button deactivateService = MainForm.CreateButton("Desactivar");
    private readonly Button reload = MainForm.CreateButton("Recargar");
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private bool ready;
    private bool busy;

    public CatalogForm(IPacienteCatalogService patients, IServicioCatalogService services)
    {
        this.patients = patients;
        this.services = services;
        Text = "Catálogos · IPS";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(910, 500);
        MinimumSize = new Size(720, 415);
        BackColor = Color.White;

        tabs.TabPages.Add(CreatePage("Pacientes", patientList,
            [("Tipo de documento", documentType), ("Número de documento", documentNumber), ("Nombre", patientName), ("Fecha de nacimiento (opcional)", birth)],
            () => ClearPatient(), SavePatientAsync, deactivatePatient, DeactivatePatientAsync));
        tabs.TabPages.Add(CreatePage("Servicios médicos", serviceList,
            [("Código", serviceCode), ("Nombre", serviceName)],
            () => ClearService(), SaveServiceAsync, deactivateService, DeactivateServiceAsync));

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(tabs, 0, 0);
        layout.Controls.Add(status, 0, 1);
        layout.Controls.Add(reload, 0, 2);
        Controls.Add(layout);

        patientList.Format += (_, args) =>
        {
            if (args.ListItem is Paciente item) args.Value = $"{item.Nombre} · {item.TipoDocumento} {item.NumeroDocumento}{InactiveLabel(item.Activo)}";
        };
        serviceList.Format += (_, args) =>
        {
            if (args.ListItem is ServicioMedico item) args.Value = $"{item.Codigo} · {item.Nombre}{InactiveLabel(item.Activo)}";
        };
        patientList.SelectedIndexChanged += (_, _) => ShowPatient();
        serviceList.SelectedIndexChanged += (_, _) => ShowService();
        reload.Click += async (_, _) => await RunAsync("Catálogos actualizados.");
        Shown += async (_, _) => await RunAsync("Catálogos cargados.");
        FormClosing += (_, args) => args.Cancel = busy;
        FormClosed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        tabs.Enabled = false;
    }

    private static TabPage CreatePage(string title, ListBox list, (string Caption, Control Input)[] fields,
        Action clear, Func<Task> save, Button deactivate, Func<Task> deactivateAction)
    {
        var page = new TabPage(title) { BackColor = Color.White };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.Controls.Add(list, 0, 0);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true, Padding = new Padding(12, 0, 0, 0) };
        foreach (var (caption, input) in fields)
        {
            editor.Controls.Add(MainForm.Caption(caption));
            editor.Controls.Add(input);
        }
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, 12, 0, 4) };
        var add = MainForm.CreateButton("Nuevo");
        var apply = MainForm.CreateButton("Guardar", primary: true);
        add.Click += (_, _) => clear();
        apply.Click += async (_, _) => await save();
        deactivate.Click += async (_, _) => await deactivateAction();
        actions.Controls.AddRange([add, apply, deactivate]);
        editor.Controls.Add(actions);
        for (var i = 0; i < editor.Controls.Count; i++)
            editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(editor, 1, 0);
        page.Controls.Add(layout);
        return page;
    }

    private static TextBox CreateInput(string name, int limit) => new() { Dock = DockStyle.Fill, AccessibleName = name, MaxLength = limit };

    private static string InactiveLabel(bool active) => active ? "" : " (inactivo)";

    private void ShowPatient()
    {
        var item = patientList.SelectedItem as Paciente;
        documentType.ReadOnly = documentNumber.ReadOnly = item is not null;
        documentType.Text = item?.TipoDocumento ?? "";
        documentNumber.Text = item?.NumeroDocumento ?? "";
        patientName.Text = item?.Nombre ?? "";
        var birthDate = item?.FechaNacimiento?.ToDateTime(TimeOnly.MinValue);
        birth.Value = birthDate >= birth.MinDate && birthDate <= birth.MaxDate ? birthDate.Value : DateTime.Today;
        birth.Checked = birthDate >= birth.MinDate && birthDate <= birth.MaxDate;
        deactivatePatient.Enabled = item?.Activo == true;
    }

    private void ShowService()
    {
        var item = serviceList.SelectedItem as ServicioMedico;
        serviceCode.Text = item?.Codigo ?? "";
        serviceName.Text = item?.Nombre ?? "";
        deactivateService.Enabled = item?.Activo == true;
    }

    private void ClearPatient()
    {
        patientList.ClearSelected();
        ShowPatient();
        documentType.Focus();
    }

    private void ClearService()
    {
        serviceList.ClearSelected();
        ShowService();
        serviceCode.Focus();
    }

    private async Task SavePatientAsync()
    {
        var current = patientList.SelectedItem as Paciente;
        var item = new Paciente(current?.Id ?? 0, documentType.Text.Trim().ToUpperInvariant(), documentNumber.Text.Trim().ToUpperInvariant(),
            patientName.Text.Trim(), birth.Checked ? DateOnly.FromDateTime(birth.Value) : null, current?.Activo ?? true);
        if (item.TipoDocumento.Length == 0 || item.NumeroDocumento.Length == 0 || item.Nombre.Length == 0)
        {
            status.Text = "Completa el tipo, el número de documento y el nombre.";
            return;
        }
        if (item.FechaNacimiento > DateOnly.FromDateTime(DateTime.Today))
        {
            status.Text = "La fecha de nacimiento debe ser hoy o anterior.";
            return;
        }
        await RunAsync("Paciente guardado.", ct => current is null ? patients.CrearAsync(item, ct) : patients.ActualizarAsync(item, ct), patientKey: (item.TipoDocumento, item.NumeroDocumento));
    }

    private async Task SaveServiceAsync()
    {
        var current = serviceList.SelectedItem as ServicioMedico;
        var item = new ServicioMedico(current?.Id ?? 0, serviceCode.Text.Trim().ToUpperInvariant(), serviceName.Text.Trim(), current?.Activo ?? true);
        if (item.Codigo.Length == 0 || item.Nombre.Length == 0)
        {
            status.Text = "Completa el código y el nombre del servicio.";
            return;
        }
        await RunAsync("Servicio guardado.", ct => current is null ? services.CrearAsync(item, ct) : services.ActualizarAsync(item, ct), serviceKey: item.Codigo);
    }

    private async Task DeactivatePatientAsync()
    {
        if (patientList.SelectedItem is Paciente { Activo: true } item && ConfirmDeactivation("paciente"))
            await RunAsync("Paciente desactivado.", ct => patients.DesactivarAsync(item.Id, ct));
    }

    private async Task DeactivateServiceAsync()
    {
        if (serviceList.SelectedItem is ServicioMedico { Activo: true } item && ConfirmDeactivation("servicio"))
            await RunAsync("Servicio desactivado.", ct => services.DesactivarAsync(item.Id, ct));
    }

    private bool ConfirmDeactivation(string entity) =>
        MessageBox.Show(this, $"¿Quieres desactivar este {entity}? El historial se conservará.", "Confirmar desactivación",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private async Task RunAsync(string success, Func<CancellationToken, Task>? change = null,
        (string Type, string Number)? patientKey = null, string? serviceKey = null)
    {
        if (busy) return;
        var selectedPatient = (patientList.SelectedItem as Paciente)?.Id;
        var selectedService = (serviceList.SelectedItem as ServicioMedico)?.Id;
        busy = true;
        tabs.Enabled = reload.Enabled = false;
        status.Text = change is null ? "Cargando catálogos…" : "Aplicando cambios…";
        var saved = false;
        try
        {
            if (change is not null)
            {
                await change(lifetime.Token);
                saved = true;
            }
            var patientItems = (await patients.ListarAsync(lifetime.Token)).ToList();
            var serviceItems = (await services.ListarAsync(lifetime.Token)).ToList();
            patientList.DataSource = patientItems;
            serviceList.DataSource = serviceItems;
            patientList.SelectedIndex = patientItems.FindIndex(item => item.Id == selectedPatient ||
                (patientKey is { } key && string.Equals(item.TipoDocumento, key.Type, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.NumeroDocumento, key.Number, StringComparison.OrdinalIgnoreCase)));
            serviceList.SelectedIndex = serviceItems.FindIndex(item => item.Id == selectedService ||
                string.Equals(item.Codigo, serviceKey, StringComparison.OrdinalIgnoreCase));
            ShowPatient();
            ShowService();
            ready = true;
            status.Text = success;
        }
        catch (CatalogDuplicateException)
        {
            status.Text = "Ya existe un registro con ese documento o código. Revisa los datos.";
        }
        catch (CatalogValidationException)
        {
            status.Text = "Revisa los campos obligatorios y conserva el documento original del paciente.";
        }
        catch (Exception error)
        {
            Log.Warning("Falló una operación de catálogo ({ErrorType})", error.GetType().Name);
            if (saved) ready = false;
            status.Text = saved
                ? "El cambio se guardó. Recarga para ver los catálogos actualizados."
                : "No pudimos completar la operación. Revisa la conexión y vuelve a intentarlo.";
        }
        finally
        {
            busy = false;
            tabs.Enabled = ready;
            reload.Enabled = true;
        }
    }
}
