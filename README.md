# IPS Atenciones médicas

Aplicación de escritorio WinForms para Windows y .NET 8. Registra una
atención con un paciente y uno o más servicios médicos activos, conserva la
auditoría y permite crear, editar y desactivar pacientes y servicios.

## Requisitos

- Windows y SDK de .NET 8 o posterior con el runtime de escritorio .NET 8.
- SQL Server Express LocalDB o Docker Desktop con contenedores Linux.
- `sqlcmd` disponible en PowerShell.

## Instalar la base de datos

Elige un perfil y ejecuta los comandos desde la raíz del repositorio. La base
`IpsAtenciones` debe estar vacía en una instalación nueva.

### LocalDB

```powershell
$server = '(localdb)\MSSQLLocalDB'
$auth = @('-E')
$env:ConnectionStrings__IpsAtenciones = "Server=$server;Database=IpsAtenciones;Integrated Security=True;TrustServerCertificate=True"
```

La identidad de Windows que crea la base se usa para ejecutar la demostración
local. El perfil Docker permite comprobar la cuenta SQL de mínimos privilegios.

### Docker

```powershell
$secure = Read-Host 'Contraseña de sa para SQL Server local' -AsSecureString
$env:IPS_SQL_PASSWORD = [Net.NetworkCredential]::new('', $secure).Password
docker compose -f deploy/docker-compose.yml up -d --wait
if ($LASTEXITCODE) { throw 'SQL Server no quedó disponible' }
$server = '127.0.0.1,1433'
$auth = @('-U', 'sa')
$env:SQLCMDPASSWORD = $env:IPS_SQL_PASSWORD
```

Compose publica el puerto solo en la interfaz de loopback. Con un volumen
existente, usa la contraseña de `sa` configurada en ese volumen.

### Scripts comunes

```powershell
sqlcmd -S $server @auth -C -b -d master -Q 'CREATE DATABASE IpsAtenciones;'
if ($LASTEXITCODE) { throw 'No se creó la base de datos' }

foreach ($script in '001-schema.sql', '002-programmability.sql', '003-seed.sql', '005-attention-integrity.sql') {
    sqlcmd -S $server @auth -C -b -d IpsAtenciones -i (Join-Path database $script)
    if ($LASTEXITCODE) { throw "Falló $script" }
}
```

Los datos iniciales son sintéticos. Para actualizar una instalación existente,
haz un respaldo y aplica 002 y 005, y 004 si usas la cuenta SQL. Si 005 detecta
una cabecera sin servicios o un detalle inconsistente, revierte la migración.
Concilia esos registros antes de volver a ejecutarla.

### Cuenta de ejecución en Docker

```powershell
$secure = Read-Host 'Contraseña de ips_atenciones_app' -AsSecureString
$appPassword = [Net.NetworkCredential]::new('', $secure).Password
$env:AppPasswordHex = [BitConverter]::ToString([Text.Encoding]::Unicode.GetBytes($appPassword)).Replace('-', '')
sqlcmd -S $server @auth -C -b -d master -i database/004-security.sql
if ($LASTEXITCODE) { throw 'No se configuró la cuenta de ejecución' }

$connection = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$connection['Data Source'] = $server
$connection['Initial Catalog'] = 'IpsAtenciones'
$connection['User ID'] = 'ips_atenciones_app'
$connection['Password'] = $appPassword
$connection['TrustServerCertificate'] = $true
$env:ConnectionStrings__IpsAtenciones = $connection.ConnectionString
Remove-Item Env:AppPasswordHex, Env:SQLCMDPASSWORD
```

Si el login ya existe, usa su contraseña actual. La cuenta ejecuta los
procedimientos autorizados y usa el tipo de tabla requerido para registrar.
La confianza en el certificado corresponde a los perfiles de desarrollo.

## Abrir la aplicación

```powershell
dotnet run --project src/IpsAtenciones.WinForms -c Release
```

Selecciona un paciente, marca uno o más servicios y guarda la atención.
Catálogos permite mantener los registros y excluye los inactivos del formulario
de atención. Para recuperar un registro con resultado incierto, usa
**Consultar resultado** en la misma sesión.

## Arquitectura

| Proyecto | Responsabilidad | Dependencias del proyecto |
|---|---|---|
| `IpsAtenciones.Domain` | Modelos de paciente, servicio y atención, invariantes y códigos de resultado. | Ninguna |
| `IpsAtenciones.Application` | Coordina el registro y define las interfaces de consultas, mantenimiento y persistencia. | Domain |
| `IpsAtenciones.Infrastructure` | Implementa las interfaces con ADO.NET y traduce los resultados y errores conocidos de SQL Server. | Application y Domain |
| `IpsAtenciones.WinForms` | Presenta el registro y los catálogos, maneja el estado de la ventana y construye las dependencias en `Program.Main`. | Application, Infrastructure y modelos de Domain |

### Patrones utilizados

- **Arquitectura limpia con puertos y adaptadores.** Las interfaces de
  persistencia se definen en Application. Infraestructura las implementa y
  WinForms las conecta al iniciar la aplicación. El caso de uso se puede
  probar con dobles sin abrir una ventana ni conectarse a SQL Server.
- **Command y handler.** `RegistrarAtencionCommand` transporta el paciente,
  la fecha, los servicios y el identificador de operación. El handler valida
  las reglas y devuelve un `RegistrarAtencionResult` tipado.
- **Repositorio y consultas.** `IAttentionRepository` concentra el registro
  y la recuperación por operación. Las consultas de pacientes y servicios
  tienen interfaces propias para la carga de catálogos.
- **Inyección por constructor.** `Program.Main` construye los adaptadores y
  los entrega al caso de uso. Los formularios reciben las dependencias que
  usan sin buscarlas en un contenedor global.

### Acceso a datos

1. El formulario envía el comando al handler, que valida el paciente y los
   servicios consultando los catálogos.
2. `SqlAttentionRepository` abre la conexión de forma asíncrona y llama a
   `dbo.usp_Atencion_Registrar` con parámetros tipados. Los servicios viajan en
   un parámetro de tabla (TVP) y se procesan juntos.
3. SQL Server valida las referencias y el estado, comprueba duplicidad y
   confirma cabecera, detalle y auditoría en una transacción. Las claves
   únicas protegen la repetición por operación y por paciente, fecha y
   servicio. La FK compuesta mantiene alineados cabecera y detalle.
4. Cuando el cliente desconoce el resultado, consulta el mismo identificador
   de operación. La GUI mantiene bloqueado el nuevo envío hasta resolverlo.

El catálogo usa procedimientos de escritura con parámetros y conserva los
registros desactivados. Serilog escribe diagnósticos con correlación en
`%LOCALAPPDATA%\IpsAtenciones\logs`.

## Verificación

```powershell
dotnet restore IpsAtenciones.slnx
dotnet build IpsAtenciones.slnx -c Release --no-restore
dotnet format IpsAtenciones.slnx --verify-no-changes --no-restore
dotnet test tests/IpsAtenciones.UnitTests -c Release --no-build --collect 'XPlat Code Coverage'
dotnet list IpsAtenciones.slnx package --vulnerable --include-transitive
```

Ejecuta la integración con `ConnectionStrings__IpsAtenciones` apuntando a una
base de prueba separada e instalada con los scripts. Las pruebas escriben datos
sintéticos.

```powershell
dotnet test tests/IpsAtenciones.IntegrationTests -c Release --no-build
```
