---
id: c4:ips-atenciones-medicas
title: Diagramas C4 de la aplicación IPS
type: c4
status: draft
updated: '2026-11-19'
---

# Diagramas C4 de la aplicación IPS

Implementación: Pending

Estado: Proposed

## Alcance y guía de lectura

Los diagramas describen la solución propuesta en tres niveles. L1 muestra los
actores y sistemas externos. L2 separa la GUI, la aplicación, la
infraestructura, SQL Server y las pruebas. L3 explica el camino de registro de
una atención. Las flechas representan llamadas o dependencias, y la dirección
de lectura es de izquierda a derecha.

## C4 nivel 1 — Contexto

```mermaid
flowchart LR
    operador["Operador de la IPS"]
    app["Sistema de Gestión de Atenciones<br/>WinForms .NET 8"]
    sql["SQL Server<br/>local o Docker"]
    docker["Docker Desktop<br/>entorno reproducible"]
    logs["Archivo de logs estructurados"]
    operador -->|"selecciona paciente y servicios<br/>y registra atención"| app
    app -->|"procedimientos almacenados<br/>parametrizados"| sql
    docker -->|"levanta y persiste"| sql
    app -->|"diagnóstico sin PII innecesaria"| logs
```

## C4 nivel 2 — Contenedores

```mermaid
flowchart LR
    user["Operador"] --> ui["WinForms UI<br/>AttentionForm y MainForm"]
    ui --> application["Application<br/>casos de uso, DTO y puertos"]
    application --> domain["Domain<br/>entidades e invariantes"]
    application --> infra["Infrastructure<br/>ADO.NET, Serilog y configuración"]
    infra --> database[("SQL Server<br/>tablas, índices y procedimientos")]
    infra --> audit["Auditoría operativa<br/>Serilog + AuditoriaAtenciones"]
    tests["UnitTests e IntegrationTests"] -.-> application
    tests -.-> domain
    tests -.-> database
```

## C4 nivel 3 — Componentes del registro

```mermaid
flowchart TB
    form["AttentionForm"] --> catalog["CatalogLoader<br/>pacientes y servicios activos"]
    patientForm["PatientCatalogForm"] --> patientHandler["PatientCatalogHandler"]
    serviceForm["ServiceCatalogForm"] --> serviceHandler["ServiceCatalogHandler"]
    form --> command["RegistrarAtencionCommand"]
    command --> handler["RegistrarAtencionHandler"]
    handler --> validator["AtencionValidator"]
    handler --> port["IAttentionRepository"]
    validator --> domain["Atencion y reglas de dominio"]
    port --> repo["SqlAttentionRepository"]
    repo --> proc["usp_Atencion_Registrar"]
    proc --> tables[("Atenciones + AtencionServicios")]
    proc --> audit[("AuditoriaAtenciones")]
    patientHandler --> catalogRepo["CatalogRepository"]
    serviceHandler --> catalogRepo
    catalogRepo --> catalogProc["usp_Paciente_* / usp_ServicioMedico_*"]
    catalogProc --> tables
    handler --> mapper["ErrorMapper"]
    mapper --> form
```

## Responsabilidades y contratos

| Componente | Responsabilidad | Contrato observable |
|---|---|---|
| `AttentionForm` | Capturar paciente, fecha y servicios | No permite guardar sin datos obligatorios |
| `PatientCatalogForm` | Crear, consultar, actualizar y desactivar pacientes | Valida documento ?nico y conserva referencias |
| `ServiceCatalogForm` | Crear, consultar, actualizar y desactivar servicios | Un servicio inactivo no aparece para nuevas atenciones |
| `CatalogLoader` | Consultar catálogos | Excluye servicios inactivos |
| `RegistrarAtencionHandler` | Orquestar validación y persistencia | Devuelve `OK` o códigos de regla estables |
| `AtencionValidator` | Aplicar reglas antes de I/O | Pruebas unitarias sin SQL Server |
| `SqlAttentionRepository` | Ejecutar procedimientos con parámetros | Nunca concatena valores del formulario |
| `usp_Atencion_Registrar` | Aplicar invariantes, transacción e índices | Rollback completo y auditoría |
| `ErrorMapper` | Convertir resultado técnico a mensaje funcional | No expone SQL ni stack trace |

## Flujo de errores

```mermaid
stateDiagram-v2
    [*] --> Edicion
    Edicion --> ValidacionLocal: Guardar
    ValidacionLocal --> Edicion: Falta paciente o servicio
    ValidacionLocal --> Persistencia: Datos válidos
    Persistencia --> Confirmada: OK
    Persistencia --> Edicion: Duplicidad o servicio inactivo
    Persistencia --> ErrorRecuperable: Timeout o SQL Server no disponible
    ErrorRecuperable --> Edicion: Reintentar
    Confirmada --> [*]
```

El diagrama permite ubicar cada mensaje en la frontera correcta. La validación
local evita viajes innecesarios, y SQL Server mantiene la autoridad cuando la
entrada llega por otra ruta o existe concurrencia.
