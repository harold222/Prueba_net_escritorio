---
id: doc:ips-atenciones-medicas-proposal
title: Propuesta sistema IPS de atenciones médicas
type: epic
status: draft
updated: '2026-11-19'
---

# Propuesta

Estado: Proposed

## Objetivo

Diseñar una aplicación WinForms para registrar atenciones médicas de una IPS,
con pacientes, servicios médicos y detalle de servicios por atención. La
solución debe demostrar arquitectura limpia, persistencia en SQL Server con
procedimientos almacenados, ejecución local y una alternativa reproducible con
Docker, pruebas unitarias y revisión de riesgos OWASP.

## Fuentes

- Petición del usuario en esta sesión, con el objetivo, las tecnologías
  permitidas, las reglas de negocio y los entregables de la prueba técnica.
- `.agents/harness/INDEX.md`, mapa de superficies del harness seleccionado.
- `.agents/harness/back-dot-net/instructions/00-csharp.instructions.md`, reglas
  aplicables a proyectos C# y archivos MSBuild.
- `.agents/harness/back-dot-net/agents/dotnet-design.agent.md`, guía de diseño
  de límites, persistencia, fallos y verificación .NET.
- `.agents/harness/back-dot-net/agents/agent-owasp-dotnet.agent.md`, controles
  de revisión de seguridad aplicables a .NET.
- `ai-harness/docs/standards/personal-lifecycle.md`, ciclo de aceptación,
  implementación y conciliación del harness personal.
- `ai-harness/harness-project-type/back-dot-net/capability.yaml`, validadores
  declarados por el paquete de stack.

Las fuentes describen el caso de negocio y las reglas del harness. La elección
de .NET 8, la separación de la tabla de detalle, el uso de ADO.NET para invocar
procedimientos y el reparto local/Docker son decisiones propuestas en el
diseño, no afirmaciones de una fuente externa.

## Alcance y no objetivos

Incluye una solucion Visual Studio para Windows con proyectos de dominio,
aplicacion, infraestructura, presentacion WinForms y pruebas. Incluye el
esquema SQL normalizado, claves e indices, procedimientos almacenados para las
operaciones de la prueba, script de creacion y datos iniciales minimos. Incluye
una ventana de registro de detalle de atencion, pantallas CRUD de pacientes y
servicios medicos, consulta de pacientes y servicios activos, manejo controlado
de errores, instrucciones para ejecucion local y con SQL Server en Docker, y
README tecnico.

La primera version no incluye autenticacion, roles, API web, aplicacion movil,
facturacion, historia clinica, archivos clinicos ni edicion o eliminacion de
atenciones. Pacientes y servicios tendran mantenimiento CRUD. La eliminacion
sera una desactivacion logica para conservar las referencias historicas.

## Requisitos y criterios de aceptación

| ID | Requisito | Criterio observable |
|---|---|---|
| REQ-001 | El sistema SHALL cargar los pacientes y servicios activos desde SQL Server para el registro. | Con registros activos e inactivos, abrir el formulario, entrar a Catálogos y volver. Solo aparecen los activos, cada servicio se lee por código y nombre y aparece una sola vez. |
| REQ-002 | El sistema SHALL permitir iniciar una atención con un paciente, una fecha de atención y uno o más servicios seleccionados. | Estado inicial con la ventana vacía. El usuario selecciona un paciente, una fecha válida y al menos un servicio. El formulario muestra el detalle elegido y permite enviar el registro. Una prueba de caso de uso verifica el comando generado. |
| REQ-003 | El sistema SHALL rechazar una atención sin paciente o sin servicios. | Estado inicial con el formulario vacío o con una sola dimensión diligenciada. Al intentar guardar se muestran mensajes de validación junto a la operación, no se ejecuta la persistencia y la base no cambia. Pruebas unitarias cubren ambas entradas inválidas. |
| REQ-004 | El sistema SHALL impedir registrar un servicio inactivo aunque el identificador sea enviado directamente al caso de uso o al procedimiento. | Estado inicial con un servicio inactivo conocido. Al intentar registrarlo, el caso de uso o la base devuelve un error de regla, no se crea atención ni detalle y la interfaz muestra una explicación accionable. Pruebas unitarias y de integración cubren el rechazo. |
| REQ-005 | El sistema SHALL impedir que un paciente tenga dos registros para la misma fecha y servicio. | Estado inicial con una atención existente para paciente, día y servicio. Al repetir el registro, la operación falla de forma controlada, conserva la primera atención y muestra un mensaje de duplicidad. Una prueba de integración ejercita la restricción bajo dos intentos consecutivos. |
| REQ-006 | El sistema SHALL conservar integridad referencial y atomicidad entre atención y sus detalles. | Estado inicial con identificadores inexistentes o un lote con una fila inválida. Al ejecutar la operación de persistencia, las FK o la transacción rechazan el dato y no queda una cabecera sin detalle ni detalles huérfanos. La prueba de base de datos comprueba PK, FK, unicidad y rollback. |
| REQ-007 | El sistema SHALL usar una arquitectura limpia por capas y procedimientos almacenados parametrizados para la persistencia. | Estado inicial de una solución nueva. La inspección de proyectos y dependencias demuestra que dominio y aplicación no dependen de WinForms ni SQL Server, infraestructura invoca procedimientos con parámetros y la presentación solo orquesta casos de uso. La revisión estática y una prueba de integración verifican el camino completo. |
| REQ-008 | El sistema SHALL presentar errores controlados y registrar diagnósticos sin exponer datos sensibles innecesarios. | Con SQL Server inaccesible o un registro duplicado, WinForms muestra en español qué ocurrió y qué puede hacer el operador, conserva su selección y permite recuperar la conexión. La prueba visual comprueba que etiquetas, acciones y mensajes sean legibles al abrir la ventana y al cambiar la escala de Windows a 125 %. Los logs no contienen la cadena de conexión ni la identificación completa. |
| REQ-009 | El sistema SHALL poder ejecutarse con SQL Server local y con SQL Server en Docker sin cambiar el código de negocio. | Estado inicial con una configuración de conexión seleccionada. Al ejecutar el perfil local o levantar el `docker compose`, el mismo ejecutable aplica el script y registra una atención. La comprobación documentada incluye comandos, versión del motor y prueba de conexión. |
| REQ-010 | El sistema SHALL incluir pruebas unitarias, verificación de seguridad y documentación de entrega. | Estado inicial con la solución compilable. `dotnet test` descubre y ejecuta las pruebas de dominio y aplicación, el escaneo de paquetes y la revisión OWASP quedan documentados, y README explica arquitectura, patrones, acceso a datos, base local y Docker. La evidencia son los reportes reales y el contenido versionado del proyecto. |
| REQ-011 | El sistema SHALL conservar una tabla de auditoría de operaciones de atención y usar índices que soporten las consultas y las reglas de unicidad. | Estado inicial con una base creada por el script. Al registrar o rechazar una atención se conserva un evento de auditoría sin secretos ni PII innecesaria. La inspección del esquema demuestra el índice único paciente-fecha-servicio, índices de consulta de catálogos e historial y un índice temporal de auditoría. La prueba de base de datos verifica que los índices y el registro se crean junto con la operación. |
| REQ-012 | El sistema SHALL permitir crear, consultar, actualizar y desactivar logicamente pacientes y servicios medicos desde la aplicacion. | Estado inicial con catalogos existentes. El usuario crea o modifica un registro valido, lo consulta en la lista y lo marca inactivo. El registro permanece disponible para preservar referencias historicas y un servicio inactivo deja de estar disponible para nuevas atenciones. Pruebas unitarias, de integracion y de interfaz cubren las operaciones y sus validaciones. |

## Restricciones y preguntas abiertas

- Se propone .NET 8 con WinForms porque es la opción permitida con soporte
  vigente y permite compartir el dominio con pruebas modernas. La aplicación
  gráfica se ejecutará en Windows. Docker alojará SQL Server y sus scripts,
  no la ventana WinForms.
- La fecha de atención se modelará como `date` de SQL Server y la unicidad se
  evaluará por día calendario. La zona horaria de la máquina Windows se usará
  solo para el valor por defecto del formulario.
- La operación de registro será transaccional y recibirá el conjunto completo
  de servicios. El nombre exacto de los procedimientos y los tipos de
  identificación del paciente se fijarán en `design.md`.
- Decision del dueno: se incluyen pantallas CRUD de pacientes y servicios. La
  eliminacion sera desactivacion logica para conservar el historial.
- Pregunta abierta para el entorno: ¿se dispone de una instancia SQL Server
  local? Recomendación: soportar LocalDB o Express en el perfil local y SQL
  Server 2022 Developer en Docker, con una cadena de conexión configurable.
- El paquete `back-dot-net` aún declara un gate de mutación. La prueba pide
  omitir mutaciones. Antes de implementar se debe decidir si se modifica la
  capacidad del harness o si el consumidor documenta la exclusión en su
  configuración de gates. No se ejecutará ni se presentará mutación como
  evidencia de esta aplicación.
- Cuando exista la solución, los `TODO` de `harness-gates.yaml` se deben
  reemplazar por comandos reales de compilación, pruebas, cobertura, auditoría
  de dependencias y Sonar. El gate de mutación permanecerá explícitamente
  fuera de alcance según la decisión del usuario.
