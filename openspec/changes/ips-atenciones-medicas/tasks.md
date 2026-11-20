---
id: doc:ips-atenciones-medicas-tasks
title: Tareas IPS de atenciones medicas
type: guide
status: draft
updated: '2026-11-19'
---

# Tareas

Cada tarea enlaza requisitos, dependencias y una comprobacion observable. Una
casilla solo se marca cuando la evidencia existe en esta sesion.

## Implementacion

- [x] T1 - REQ-007, REQ-009, REQ-010. Solucion .NET 8 WinForms, Clean Architecture, pruebas y perfiles local/Docker. Evidencia: build Release correcto y referencias revisadas.
- [x] T2 - REQ-001, REQ-002, REQ-003, REQ-004, REQ-005. Entidades, comandos, validaciones, puertos y handler. Evidencia: 8 pruebas unitarias Release para casos validos, campos ausentes, paciente inactivo, fecha futura, servicio inactivo, servicio repetido y correlacion generada.
- [x] T3 - REQ-006, REQ-011. Ejecutar database/001-schema.sql en SQL Server limpio y comprobar FK, claves, indices y SchemaVersion. Evidencia: SQL Server Docker saludable, scripts 001-003 ejecutados, FK e indices consultados con sqlcmd.
- [ ] T4 - REQ-004, REQ-005, REQ-006, REQ-011. Impedir cabeceras con TVP vacío y exigir FK compuesta para el detalle. Cubrir referencias, rollback, duplicidad, auditoría, idempotencia y todos los momentos de cancelación en integración.
- [ ] T5 - REQ-001, REQ-002, REQ-003, REQ-008. Corregir distribución, nombres de servicios, recarga, mensajes y recuperación de conexión. Repetir pruebas de registro, selección, escala y navegación por teclado con los dos perfiles.
- [ ] T6 - REQ-008, REQ-010. Otorgar `EXECUTE` y `REFERENCES` sobre el tipo de tabla en el script 004 y validar mínimos privilegios con la cuenta creada por el script. Verificar redacción en los flujos de registro y mantenimiento.
- [ ] T7 - REQ-009, REQ-010. Repetir la instalación limpia con el script 004 corregido y documentar los comandos reproducibles para LocalDB y Docker.
- [ ] T8 - REQ-012. Completar los estados y validaciones del formulario CRUD. Verificar actualización, inmutabilidad del documento y desactivación lógica en UI y SQL.
- [ ] T9 - REQ-010. Completar cobertura de líneas y ramas del código propio según el umbral del harness. Conservar los resultados de pruebas y del escaneo de paquetes.

## Verificacion

- [x] V1 - REQ-001 a REQ-012. dotnet format --verify-no-changes, dotnet build y dotnet test en Release. Evidencia: formato sin cambios, build sin advertencias ni errores y 8 pruebas correctas.
- [ ] V2 - REQ-005, REQ-006, REQ-011. Confirmar con SQL Server que las invariantes de detalle y lote vacío quedan protegidas después de la corrección.
- [ ] V3 - REQ-008, REQ-010. Cerrar los hallazgos de seguridad y repetir la revisión OWASP. SCA ejecutado sin vulnerabilidades reportadas.
- [ ] V4 - REQ-009, REQ-010. Repetir el recorrido completo con las instrucciones finales del README. LocalDB y Docker ya se probaron en bases limpias con los resultados detallados abajo.
- [x] V5 - REQ-010. Mantener gates cableados con comandos reales de build, pruebas, cobertura y auditoria. Sonar requiere servidor del consumidor y mutacion queda fuera de alcance.

## Revision por PR, promocion y flip

- [x] P1. Ejecutar python ai-harness/scripts/spec_gate.py validate --change openspec/changes/ips-atenciones-medicas. Validacion estructural correcta; la aceptacion por PR sigue pendiente.
- [ ] P2. Completar la revisión semántica independiente. El contraste local con el código y el contexto ya quedó incorporado al diseño.
- [ ] P3. Obtener aceptacion por PR de diseno y promocion con referencias reales.
- [ ] P4. Ejecutar implementation-ready con PRs reales y la autorizacion de implementacion de esta sesion.
- [ ] P5. Tras merge del codigo, ejecutar implemented, conciliar system y ejecutar reconciled.
- [ ] P6. Ejecutar archive-check solo cuando tareas y evidencias esten completas.

## Progreso de la reanudación

- Completado. `harness-doctor`, `harness-verify` y `spec-check` ejecutados correctamente. El error UTF-8 del handoff ya no se reproduce. El gate estructural sigue requiriendo aceptación por PR para avanzar a implementación.
- Completado. Compilación Release con cero advertencias y errores, formato sin cambios, 8 pruebas unitarias aprobadas y reporte Cobertura XML generado. NuGet no reportó vulnerabilidades directas ni transitivas en los orígenes consultados.
- Completado. SQL Server en Docker reiniciado con `up -d --wait` y estado `healthy`. Se conserva la base existente.
- Completado en parte (T5). Computer Use cargó `Paciente demo` y excluyó el servicio inactivo con LocalDB. Marcó el servicio activo, habilitó Guardar, registró la atención `2` y reprodujo el rechazo consecutivo. Confirmó cuatro defectos visibles: botones recortados, servicio mostrado como representación de C#, mensaje `DuplicateAttention` y servicio duplicado al cerrar Catálogos. Sigue la prueba de desconexión y recuperación.
- Completado en parte (T4/T7/V4). Scripts 001–003 instalados en bases desechables de Docker y LocalDB, con reaplicación de la semilla. En LocalDB pasaron las tres pruebas de integración existentes. En Docker con la cuenta de ejecución, pasaron dos y falló el registro. La cuenta carece de `EXECUTE` sobre el tipo `dbo.AtencionServicioIdList` (SQL 229) y tampoco tiene acceso directo a las tablas. Se comprobará la corrección de permisos en la base desechable.
- Completado en parte (T6/V3). Pasó la inspección estática de llamadas de log. Queda comprobar el contenido y los errores de ejecución. Cobertura unitaria: 18/22 líneas (81,81 %) y 17/22 ramas (77,27 %) en los ensamblados instrumentados de Domain y Application. Infraestructura y WinForms quedan fuera de esa medición.
- Completado en parte (T4/V2). Con un grant temporal en la base Docker desechable pasaron las tres pruebas de integración usando la cuenta limitada. SQL confirmó idempotencia, un ganador concurrente, auditoría para éxito, duplicidad, referencia inexistente y servicio inactivo, y cero cabeceras de los intentos rechazados. También confirmó dos brechas: una llamada con TVP vacío creó una cabecera y una fecha de detalle distinta de la cabecera superó las FK existentes. El segundo ensayo se revirtió.
- Completado en parte (T5/T6). Computer Use comprobó que, con la base desechable inaccesible, guardar mostró «No fue posible registrar la atención» y conservó la selección. Al volver a poner la base en línea, la misma sesión confirmó la regla de duplicidad. El log incluyó el nombre de la base. En el evento revisado no aparecieron el documento de prueba ni marcadores de contraseña o cadena de conexión.
- Completado en parte (T8). SQL confirmó creación, actualización y desactivación de paciente y servicio de prueba. Los registros desactivados permanecieron en catálogo y el servicio quedó fuera de la consulta de activos. Falta repetir el CRUD en la interfaz corregida.
- En curso (P2/P3). Rama base `origin/master` publicada en `5b5d1ed`. La rama de diseño incluye el paquete OpenSpec. La aceptación, promoción y verificación del gate siguen pendientes.
