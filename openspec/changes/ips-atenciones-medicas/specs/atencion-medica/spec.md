---
id: doc:ips-atenciones-medicas-spec
title: Spec de registro de atenciones médicas
type: rf
status: draft
updated: '2026-11-19'
---

## Purpose

Definir el comportamiento observable del registro de atenciones en la
aplicación WinForms y las invariantes que SQL Server debe conservar.

## ADDED Requirements

### Requirement: Catálogos consultables (REQ-001)

El sistema SHALL cargar pacientes y servicios médicos activos desde SQL Server
para el registro de atenciones.

#### Scenario: Carga inicial de catálogos

- **WHEN** el usuario abre el registro de atención con datos de catálogo
  disponibles
- **THEN** la ventana muestra pacientes y solo servicios activos, indicando un
  estado de carga o un error recuperable si una consulta falla

#### Scenario: Servicio inactivo no seleccionable

- **WHEN** existe un servicio con `Activo = 0` y el usuario abre o actualiza la
  lista de servicios
- **THEN** ese servicio no aparece como opción seleccionable

#### Scenario: Regreso desde Catálogos

- **WHEN** el usuario abre Catálogos, desactiva un servicio y vuelve al registro
- **THEN** la lista refleja los cambios, muestra cada servicio activo una sola
  vez por código y nombre y conserva solo las selecciones que siguen vigentes

### Requirement: Composición de una atención (REQ-002)

El sistema SHALL permitir seleccionar un paciente, una fecha de atención y uno
o más servicios activos antes de enviar el registro.

#### Scenario: Formulario válido

- **WHEN** el usuario selecciona un paciente, una fecha válida y al menos un
  servicio activo
- **THEN** la ventana muestra el detalle seleccionado y habilita la acción de
  guardar

#### Scenario: Servicio repetido dentro del formulario

- **WHEN** el usuario marca, desmarca y vuelve a marcar un servicio
- **THEN** la lista conserva una fila y el comando incluye una sola vez el servicio

### Requirement: Validación de campos obligatorios (REQ-003)

El sistema SHALL rechazar el guardado si falta el paciente o si el detalle no
contiene servicios.

#### Scenario: Paciente ausente

- **WHEN** el usuario intenta guardar con fecha y servicios pero sin paciente
- **THEN** se muestra un mensaje de validación, no se llama al repositorio y no
  cambia la base de datos

#### Scenario: Servicios ausentes

- **WHEN** el usuario intenta guardar con paciente y fecha pero sin servicios
- **THEN** se muestra un mensaje de validación, no se llama al repositorio y no
  cambia la base de datos

### Requirement: Exclusión de servicios inactivos (REQ-004)

El sistema SHALL validar el estado activo en el caso de uso y en la operación
transaccional de SQL Server. Un identificador inactivo enviado fuera de la UI
SHALL ser rechazado.

#### Scenario: Rechazo por caso de uso

- **WHEN** un comando contiene un servicio inactivo
- **THEN** el caso de uso devuelve un error de regla y no solicita persistencia

#### Scenario: Rechazo por persistencia

- **WHEN** una llamada directa al procedimiento contiene un servicio inactivo
- **THEN** el procedimiento revierte la transacción y devuelve el código de
  error de servicio inactivo

### Requirement: Unicidad por paciente, día y servicio (REQ-005)

El sistema SHALL impedir más de una relación entre el mismo paciente, fecha y
servicio, incluso cuando dos intentos lleguen consecutivamente a SQL Server.

#### Scenario: Registro duplicado

- **WHEN** ya existe una relación para paciente, fecha y servicio y el usuario
  intenta guardarla de nuevo
- **THEN** la primera relación permanece, la segunda se rechaza con un mensaje
  de duplicidad y la ventana conserva los datos para corregirlos

#### Scenario: Varios servicios en una atención

- **WHEN** la atención contiene dos servicios activos que no tienen duplicidad
  previa
- **THEN** se guardan la cabecera y ambas relaciones en una sola transacción

### Requirement: Integridad y atomicidad de datos (REQ-006)

El esquema SHALL usar PK, FK, `NOT NULL` donde corresponda, una restricción de
unicidad y una transacción que impida cabeceras sin detalle o detalles huérfanos.

#### Scenario: Identificador inexistente

- **WHEN** se intenta guardar un paciente o servicio inexistente
- **THEN** la FK o la validación del procedimiento rechaza la operación y no
  queda ninguna fila parcial

#### Scenario: Error en una fila del lote

- **WHEN** una de varias filas de detalle no es válida durante el guardado
- **THEN** el procedimiento revierte también las filas válidas del mismo lote

### Requirement: Separación de capas y acceso parametrizado (REQ-007)

El sistema SHALL mantener dominio y aplicación independientes de WinForms y
SQL Server. Infraestructura SHALL invocar procedimientos almacenados con
parámetros y la presentación SHALL depender de casos de uso o interfaces de
aplicación.

#### Scenario: Inspección de referencias

- **WHEN** se compila la solución y se inspeccionan las referencias de los
  proyectos
- **THEN** dominio no referencia infraestructura ni presentación, aplicación
  no referencia WinForms y las consultas no concatenan valores del usuario

### Requirement: Errores controlados y diagnóstico (REQ-008)

El sistema SHALL transformar errores de validación, duplicidad, conexión y
timeout en mensajes funcionales. Los logs SHALL ser estructurados y no incluir
contraseñas, cadenas de conexión ni la identificación completa del paciente.

#### Scenario: SQL Server no disponible

- **WHEN** el usuario intenta cargar o guardar con el servidor detenido
- **THEN** la aplicación muestra un error de disponibilidad, conserva la
  ventana utilizable y registra el evento técnico sin secretos. Cuando vuelve
  la conexión, la acción de reintentar la carga actualiza los catálogos sin
  reiniciar la aplicación

#### Scenario: Duplicidad de negocio

- **WHEN** SQL Server devuelve el código de duplicidad del procedimiento
- **THEN** la aplicación muestra un mensaje de regla de negocio y no un stack
  trace ni un mensaje crudo del proveedor

#### Scenario: Distribución y navegación de formularios

- **WHEN** el operador abre Registro o Catálogos a escalas de Windows de 100 %
  y 125 % y llega a sus controles mediante Tab
- **THEN** etiquetas, botones y estado se leen completos al tamaño mínimo de
  ventana y el foco recorre los controles en el orden de trabajo

### Requirement: Ejecución local y Docker (REQ-009)

El sistema SHALL permitir configurar la cadena de conexión mediante perfiles
local y Docker. El código de dominio y aplicación SHALL ser idéntico en ambos.

#### Scenario: Perfil Docker

- **WHEN** el usuario levanta SQL Server con `docker compose` y aplica el script
  de base de datos
- **THEN** la aplicación WinForms se conecta con el perfil Docker y puede
  completar el escenario válido de registro

#### Scenario: Perfil local

- **WHEN** el usuario configura una instancia local compatible y ejecuta el
  mismo script
- **THEN** la misma solución completa el escenario válido sin recompilar las
  capas de dominio y aplicación

### Requirement: Verificación y documentación (REQ-010)

El proyecto SHALL incluir pruebas unitarias ejecutables, revisión OWASP
documentada, script SQL, instrucciones de Docker y README de arquitectura,
patrones y acceso a datos.

#### Scenario: Pruebas unitarias

- **WHEN** se ejecuta `dotnet test` desde la solución
- **THEN** se descubren pruebas de reglas y errores y el comando produce un
  resultado verificable

#### Scenario: Entrega reproducible

- **WHEN** una persona sigue el README desde un checkout limpio con los
  prerrequisitos declarados
- **THEN** puede compilar, levantar la base de datos, ejecutar las pruebas y
  abrir la aplicación siguiendo los dos perfiles documentados

### Requirement: Auditoría e índices (REQ-011)

El sistema SHALL mantener una tabla append-only de auditoría para las
operaciones de atención y SHALL definir índices para las búsquedas de catálogo,
historial, auditoría y la unicidad paciente-fecha-servicio.

#### Scenario: Auditoría de registro exitoso

- **WHEN** el procedimiento registra una atención y sus servicios
- **THEN** la misma transacción inserta un evento de auditoría con operación,
  resultado, fecha UTC, identificadores técnicos y estación, sin guardar
  contraseña, cadena de conexión ni texto clínico

#### Scenario: Auditoría de rechazo

- **WHEN** el procedimiento rechaza una solicitud por duplicidad o servicio
  inactivo
- **THEN** se conserva un evento de auditoría con el código de rechazo sin
  alterar las tablas de atención ni de detalle

#### Scenario: Índices declarados

- **WHEN** se inspecciona el script de base de datos
- **THEN** existe una restricción o índice único para paciente-fecha-servicio,
  índices para pacientes, servicios activos, atenciones por paciente y fecha,
  servicios por atención y auditoría por fecha y operación

### Requirement: Administracion de catalogos (REQ-012)

El sistema SHALL permitir crear, consultar, actualizar y desactivar logicamente
pacientes y servicios medicos. Una desactivacion SHALL conservar las referencias
historicas y excluir servicios inactivos de nuevos registros.

#### Scenario: Crear paciente o servicio

- **WHEN** el usuario diligencia los campos obligatorios con valores validos y
  guarda el formulario de catalogo
- **THEN** el sistema crea el registro, lo muestra en la lista y registra el
  resultado de la operacion

#### Scenario: Actualizar catalogo

- **WHEN** el usuario modifica un registro existente con valores validos
- **THEN** el sistema guarda los cambios, conserva el identificador y refresca
  la lista sin duplicar el registro

#### Scenario: Desactivar un registro

- **WHEN** el usuario marca paciente o servicio como inactivo
- **THEN** el registro permanece en la base para las relaciones historicas, deja
  de ofrecerse como servicio para nuevas atenciones y la lista refleja su
  estado

#### Scenario: Duplicado o dato invalido en catalogo

- **WHEN** el usuario guarda un documento o codigo que ya existe, o deja un
  campo obligatorio vacio
- **THEN** el sistema rechaza la operacion con un mensaje funcional y conserva
  los datos anteriores
