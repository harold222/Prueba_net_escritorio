IF TYPE_ID(N'dbo.AtencionServicioIdList') IS NULL
    EXEC(N'CREATE TYPE dbo.AtencionServicioIdList AS TABLE (ServicioMedicoId int NOT NULL PRIMARY KEY)');
GO

CREATE OR ALTER PROCEDURE dbo.usp_Paciente_Listar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PacienteId, TipoDocumento, NumeroDocumento, Nombre, FechaNacimiento, Activo
    FROM dbo.Pacientes
    ORDER BY Nombre, PacienteId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Paciente_Crear
    @TipoDocumento varchar(20), @NumeroDocumento varchar(40), @Nombre nvarchar(160), @FechaNacimiento date = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(TRIM(@TipoDocumento), '') IS NULL OR NULLIF(TRIM(@NumeroDocumento), '') IS NULL
       OR NULLIF(TRIM(@Nombre), '') IS NULL OR @FechaNacimiento > CONVERT(date, SYSDATETIME())
        THROW 50001, 'Invalid patient data.', 1;

    INSERT dbo.Pacientes (TipoDocumento, NumeroDocumento, Nombre, FechaNacimiento)
    VALUES (UPPER(TRIM(@TipoDocumento)), UPPER(TRIM(@NumeroDocumento)), TRIM(@Nombre), @FechaNacimiento);
    SELECT CONVERT(int, SCOPE_IDENTITY()) AS PacienteId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Paciente_Actualizar
    @PacienteId int, @TipoDocumento varchar(20), @NumeroDocumento varchar(40), @Nombre nvarchar(160), @FechaNacimiento date = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(TRIM(@Nombre), '') IS NULL OR @FechaNacimiento > CONVERT(date, SYSDATETIME())
        THROW 50001, 'Invalid patient data.', 1;

    UPDATE dbo.Pacientes
    SET Nombre = TRIM(@Nombre), FechaNacimiento = @FechaNacimiento
    WHERE PacienteId = @PacienteId AND TipoDocumento = UPPER(TRIM(@TipoDocumento))
        AND NumeroDocumento = UPPER(TRIM(@NumeroDocumento));
    IF @@ROWCOUNT = 0
        THROW 50002, 'Patient does not exist or the document has changed.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Paciente_Desactivar @PacienteId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Pacientes SET Activo = 0 WHERE PacienteId = @PacienteId;
    IF @@ROWCOUNT = 0
        THROW 50002, 'Patient does not exist.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ServicioMedico_ListarActivos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ServicioMedicoId, Codigo, Nombre, Activo
    FROM dbo.ServiciosMedicos WHERE Activo = 1 ORDER BY Nombre, ServicioMedicoId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ServicioMedico_Listar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ServicioMedicoId, Codigo, Nombre, Activo
    FROM dbo.ServiciosMedicos ORDER BY Nombre, ServicioMedicoId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ServicioMedico_Crear @Codigo varchar(30), @Nombre nvarchar(160)
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(TRIM(@Codigo), '') IS NULL OR NULLIF(TRIM(@Nombre), '') IS NULL
        THROW 50001, 'Invalid medical service data.', 1;

    INSERT dbo.ServiciosMedicos (Codigo, Nombre) VALUES (UPPER(TRIM(@Codigo)), TRIM(@Nombre));
    SELECT CONVERT(int, SCOPE_IDENTITY()) AS ServicioMedicoId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ServicioMedico_Actualizar @ServicioMedicoId int, @Codigo varchar(30), @Nombre nvarchar(160)
AS
BEGIN
    SET NOCOUNT ON;
    IF NULLIF(TRIM(@Codigo), '') IS NULL OR NULLIF(TRIM(@Nombre), '') IS NULL
        THROW 50001, 'Invalid medical service data.', 1;

    UPDATE dbo.ServiciosMedicos
    SET Codigo = UPPER(TRIM(@Codigo)), Nombre = TRIM(@Nombre)
    WHERE ServicioMedicoId = @ServicioMedicoId;
    IF @@ROWCOUNT = 0
        THROW 50002, 'Medical service does not exist.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ServicioMedico_Desactivar @ServicioMedicoId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ServiciosMedicos SET Activo = 0 WHERE ServicioMedicoId = @ServicioMedicoId;
    IF @@ROWCOUNT = 0
        THROW 50002, 'Medical service does not exist.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Atencion_Registrar
    @PacienteId int,
    @FechaAtencion date,
    @OperacionId uniqueidentifier,
    @Servicios dbo.AtencionServicioIdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @resultado varchar(40) = 'OK', @id int, @pacienteActivo bit;
    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @pacienteActivo = Activo FROM dbo.Pacientes WITH (UPDLOCK, HOLDLOCK) WHERE PacienteId = @PacienteId;
        SELECT @id = AtencionId FROM dbo.Atenciones WITH (UPDLOCK, HOLDLOCK) WHERE OperacionId = @OperacionId;
        IF @id IS NULL
            SELECT TOP (1) @resultado = Resultado
            FROM dbo.AuditoriaAtenciones
            WHERE OperacionId = @OperacionId AND Operacion = 'REGISTRAR'
            ORDER BY AuditoriaId DESC;

        IF @id IS NOT NULL OR @resultado <> 'OK'
        BEGIN
            COMMIT;
            SELECT @resultado AS CodigoResultado, @id AS AtencionId;
            RETURN;
        END;

        IF @PacienteId IS NULL OR @PacienteId <= 0
            SET @resultado = 'PATIENT_REQUIRED';
        ELSE IF @pacienteActivo IS NULL
            SET @resultado = 'REFERENCE_NOT_FOUND';
        ELSE IF @pacienteActivo = 0
            SET @resultado = 'PATIENT_INACTIVE';
        ELSE IF @FechaAtencion IS NULL OR @OperacionId IS NULL
            SET @resultado = 'VALIDATION_ERROR';
        ELSE IF NOT EXISTS (SELECT 1 FROM @Servicios)
            SET @resultado = 'SERVICE_REQUIRED';
        ELSE IF EXISTS (
            SELECT 1 FROM @Servicios AS s
            LEFT JOIN dbo.ServiciosMedicos AS m ON m.ServicioMedicoId = s.ServicioMedicoId
            WHERE m.ServicioMedicoId IS NULL)
            SET @resultado = 'REFERENCE_NOT_FOUND';
        ELSE IF EXISTS (
            SELECT 1 FROM @Servicios AS s
            JOIN dbo.ServiciosMedicos AS m ON m.ServicioMedicoId = s.ServicioMedicoId
            WHERE m.Activo = 0)
            SET @resultado = 'SERVICE_INACTIVE';
        ELSE IF EXISTS (
            SELECT 1 FROM @Servicios AS s
            JOIN dbo.AtencionServicios AS d WITH (UPDLOCK, HOLDLOCK)
                ON d.PacienteId = @PacienteId AND d.FechaAtencion = @FechaAtencion AND d.ServicioMedicoId = s.ServicioMedicoId)
            SET @resultado = 'DUPLICATE_ATTENTION';

        IF @resultado = 'OK'
        BEGIN
            INSERT dbo.Atenciones (PacienteId, FechaAtencion, OperacionId)
            VALUES (@PacienteId, @FechaAtencion, @OperacionId);
            SET @id = CONVERT(int, SCOPE_IDENTITY());

            INSERT dbo.AtencionServicios (AtencionId, PacienteId, FechaAtencion, ServicioMedicoId)
            SELECT @id, @PacienteId, @FechaAtencion, ServicioMedicoId FROM @Servicios;
        END;

        -- The validation branch writes only an audit event. The success branch
        -- commits that event and every business row together.
        INSERT dbo.AuditoriaAtenciones (Operacion, Resultado, AtencionId, PacienteId, OperacionId, Estacion)
        VALUES ('REGISTRAR', @resultado, @id, @PacienteId, @OperacionId, HOST_NAME());
        COMMIT;
        SELECT @resultado AS CodigoResultado, @id AS AtencionId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Atencion_ConsultarPorOperacion @OperacionId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) Resultado AS CodigoResultado, AtencionId
    FROM dbo.AuditoriaAtenciones
    WHERE OperacionId = @OperacionId AND Operacion = 'REGISTRAR'
    ORDER BY AuditoriaId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Atencion_Listar @PacienteId int, @Desde date, @Hasta date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AtencionId, a.PacienteId, a.FechaAtencion, a.RegistradaUtc,
           s.ServicioMedicoId, s.Codigo, s.Nombre
    FROM dbo.Atenciones AS a
    JOIN dbo.AtencionServicios AS d ON d.AtencionId = a.AtencionId
    JOIN dbo.ServiciosMedicos AS s ON s.ServicioMedicoId = d.ServicioMedicoId
    WHERE a.PacienteId = @PacienteId AND a.FechaAtencion BETWEEN @Desde AND @Hasta
    ORDER BY a.FechaAtencion DESC, a.AtencionId DESC, s.Codigo;
END;
GO
