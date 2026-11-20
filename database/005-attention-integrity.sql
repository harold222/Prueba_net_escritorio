SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (
    SELECT 1 FROM dbo.Atenciones AS a
    WHERE NOT EXISTS (SELECT 1 FROM dbo.AtencionServicios AS d WHERE d.AtencionId = a.AtencionId))
    THROW 50003, 'Existing attention headers without services must be reconciled before migration.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID('dbo.Atenciones') AND name = 'UQ_Atencion_Cabecera')
    ALTER TABLE dbo.Atenciones ADD CONSTRAINT UQ_Atencion_Cabecera UNIQUE (AtencionId, PacienteId, FechaAtencion);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.AtencionServicios') AND name = 'FK_Detalle_CabeceraCompleta')
    ALTER TABLE dbo.AtencionServicios WITH CHECK ADD CONSTRAINT FK_Detalle_CabeceraCompleta
        FOREIGN KEY (AtencionId, PacienteId, FechaAtencion)
        REFERENCES dbo.Atenciones (AtencionId, PacienteId, FechaAtencion);

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.AtencionServicios') AND name = 'FK_Detalle_Cabecera')
    ALTER TABLE dbo.AtencionServicios DROP CONSTRAINT FK_Detalle_Cabecera;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AuditoriaAtenciones') AND name = 'IX_Auditoria_Operacion')
    CREATE INDEX IX_Auditoria_Operacion ON dbo.AuditoriaAtenciones (OperacionId, Operacion, AuditoriaId DESC)
        INCLUDE (Resultado, AtencionId);

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 2)
    INSERT dbo.SchemaVersion (VersionNumber) VALUES (2);

COMMIT;
