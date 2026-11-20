using IpsAtenciones.Application;
using IpsAtenciones.Infrastructure;
using Serilog;
using System.Windows.Forms;
namespace IpsAtenciones.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IpsAtenciones",
            "logs");
        Directory.CreateDirectory(logDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        ApplicationConfiguration.Initialize();
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__IpsAtenciones");
        SqlCatalogService? catalogs = string.IsNullOrWhiteSpace(cs) ? null : new SqlCatalogService(cs);
        RegistrarAtencionHandler? handler = string.IsNullOrWhiteSpace(cs) ? null : new(new SqlPacienteQuery(cs), new SqlServicioQuery(cs), new SqlAttentionRepository(cs));
        try
        {
            global::System.Windows.Forms.Application.Run(new MainForm(handler, catalogs));
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
