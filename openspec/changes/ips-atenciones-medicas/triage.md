---
id: doc:ips-atenciones-medicas-triage
title: Triage IPS de atenciones médicas
type: guide
status: draft
updated: '2026-11-19'
---

# Triage

Nivel: A

## Nivel A/B/C y justificación

El cambio es nivel A porque introduce una aplicación completa, un modelo de
datos con invariantes de negocio, una arquitectura de ejecución y controles de
seguridad. Afecta la futura solución Visual Studio, el script SQL y la guía de
operación. El diseño incorpora hallazgos del prototipo local. Los destinos
de promoción y conciliación son archivos nuevos del repositorio consumidor.

## Aplicabilidad

| Artefacto | Aplica | Motivo | Candidato | Destino previo | Canónico final | before_sha256 |
|---|---|---|---|---|---|---|
| Propuesta | Sí | Requisitos de la prueba y límites del MVP | `proposal.md` | `docs/designs/ips-atenciones-medicas/proposal.md` | — | — |
| Spec funcional | Sí | Escenarios comprobables de registro y errores | `specs/atencion-medica/spec.md` | `docs/designs/ips-atenciones-medicas/specs/atencion-medica/spec.md` | — | — |
| Triage | Sí | Matriz de promoción y aplicabilidad del paquete | `triage.md` | `docs/designs/ips-atenciones-medicas/triage.md` | — | — |
| Diseño técnico | Sí | Arquitectura, datos, despliegue y verificación | `design.md` | `docs/designs/ips-atenciones-medicas/design.md` | — | — |
| Tareas | Sí | Trazabilidad REQ, implementación y evidencia | `tasks.md` | `docs/designs/ips-atenciones-medicas/tasks.md` | — | — |
| ADR arquitectura | Sí | Decisión de Clean Architecture para WinForms | `adr/001-clean-architecture-winforms.md` | `docs/designs/ips-atenciones-medicas/adr/001-clean-architecture-winforms.md` | `docs/system/ips-atenciones-medicas/adr/001-clean-architecture-winforms.md` | — |
| ADR persistencia | Sí | Procedimientos almacenados y transacción SQL Server | `adr/002-sql-server-stored-procs.md` | `docs/designs/ips-atenciones-medicas/adr/002-sql-server-stored-procs.md` | `docs/system/ips-atenciones-medicas/adr/002-sql-server-stored-procs.md` | — |
| ADR ejecución | Sí | Diferencia entre host WinForms y SQL Server local/Docker | `adr/003-local-docker-runtime.md` | `docs/designs/ips-atenciones-medicas/adr/003-local-docker-runtime.md` | `docs/system/ips-atenciones-medicas/adr/003-local-docker-runtime.md` | — |
| Candidato modelo de datos | Sí | El esquema y sus invariantes deben quedar trazados | `system/candidates/bd/modelo-datos.md` | `docs/designs/ips-atenciones-medicas/candidates/bd/modelo-datos.md` | `docs/system/ips-atenciones-medicas/bd/modelo-datos.md` | — |
| Candidato amenazas | Sí | La petición exige revisión OWASP | `system/candidates/threatmodeling/ips-threat-model.md` | `docs/designs/ips-atenciones-medicas/candidates/threatmodeling/ips-threat-model.md` | `docs/system/ips-atenciones-medicas/threatmodeling/ips-threat-model.md` | — |
| Candidato C4 | Sí | Contexto, contenedores y componentes deben poder leerse visualmente | `system/candidates/c4/ips-c4.md` | `docs/designs/ips-atenciones-medicas/candidates/c4/ips-c4.md` | `docs/system/ips-atenciones-medicas/c4/ips-c4.md` | — |
| Candidato API | Sí | Contrato de aplicación y ejemplo de integración | `system/candidates/api/ips-atenciones-api.md` | `docs/designs/ips-atenciones-medicas/candidates/api/ips-atenciones-api.md` | `docs/system/ips-atenciones-medicas/api/ips-atenciones-api.md` | — |

No se promueven a `system` documentos vigentes en esta fase. Los cuatro
candidatos de sistema permanecen con `Implementación: Pending` hasta que el
código y las pruebas reales permitan conciliarlos. `before_sha256` queda vacío
porque todos los destinos propuestos son nuevos.

## Decisiones que requieren ADR

- `adr/001-clean-architecture-winforms.md` fija los límites entre dominio,
  aplicación, infraestructura y presentación.
- `adr/002-sql-server-stored-procs.md` fija el contrato transaccional y la
  defensa duplicada de las reglas en aplicación y base de datos.
- `adr/003-local-docker-runtime.md` fija la topología operativa de Windows y
  SQL Server local o en contenedor.

La matriz cubre todos los Markdown del paquete y sus destinos iniciales. Los
enlaces internos se mantienen dentro del paquete para que el gate pueda
comprobarlos antes de cualquier promoción.
