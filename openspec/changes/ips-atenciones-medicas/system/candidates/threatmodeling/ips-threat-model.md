---
id: doc:ips-atenciones-medicas-candidate-threat-model
title: Candidato de amenazas IPS de escritorio
type: guide
status: draft
updated: '2026-11-19'
---

# Candidato de amenazas IPS de escritorio

Implementación: Pending

Estado: Proposed

## Estado

Candidato para revisión OWASP posterior al código. Los flujos relevantes son
la configuración de conexión, la selección de identificadores en WinForms, la
invocación de procedimientos y la escritura de logs y auditoría.

## Controles propuestos

- Parámetros ADO.NET y permisos de `EXECUTE` para reducir inyección y acceso
  directo a tablas.
- Secretos fuera del repositorio y redacción de PII en Serilog.
- Timeouts explícitos, mensajes funcionales y no exposición de excepciones.
- Auditoría append-only con códigos de resultado y sin documentos completos.
- SCA de paquetes NuGet y análisis estático separados de la revisión manual.

La aplicabilidad de CORS, autenticación HTTP y SSRF se confirmará como no
aplicable solo después de verificar que no se agregue una superficie web.
